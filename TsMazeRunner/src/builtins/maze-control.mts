/**
 * Builtin: Maze Player Control Functions
 *
 * Provides helper functions to control the player character in a maze scene.
 * Uses absolute compass directions (north/south/east/west) for movement.
 * Camera is top-down overhead, so directions on screen are constant.
 * All functions interact with the C# MazePlayerBridge via PuerTS.
 */

// ---- Summary for tool description (always in context) ----

export const summary = `**maze-control** — Control the player in the maze. \`getMazeMap()\` returns a full ASCII map of the maze with walls, player (P), and goal (G) — use it to plan the complete route. \`movePath([{dir, steps}, ...])\` executes a **multi-segment path** in one call. \`getPlayerStatus()\` returns position and obstacle distances. Read \`.description\` for details.`;

// ---- Description for on-demand access via import ----

export const description = `
- **\`movePath(segments)\`** — Move the player along a **multi-segment planned path** in a single call. Steps are in **grid cells** (each cell = 2m on the ground, shown by green grid lines).
  - \`segments\` (array, required): Array of objects \`{ dir: string, steps: number }\`.
    - \`dir\`: compass direction — "north", "south", "east", or "west"
    - \`steps\`: number of grid cells **to move** (relative displacement, NOT an absolute coordinate!) (1–10, integer). E.g. if you are at column 1 and want to reach column 3, steps = 3 − 1 = **2**, not 3.
  - You can plan the full route at once or break it into batches. Max 20 segments per call.
  - The player always lands exactly at a cell center (snaps to grid).
  - Returns: \`{ success, stepsRequested, stepsCompleted, blocked, reachedGoal, totalDistanceMoved, position, message }\`
  - Executes each segment sequentially. Stops early if blocked by a wall or if the goal is reached.
  - **You MUST walk INTO the goal cell (G) — do not stop one cell away!** The \`reachedGoal\` flag only becomes \`true\` when the player is **inside** the goal cell.

  **⚠️ FORBIDDEN CODE:** You may ONLY write code that calls \`getMazeMap()\`, \`movePath()\`, \`getPlayerStatus()\`, or \`captureScreenshot()\`. Do NOT write code to parse/index/analyze the ASCII map string, search for characters, or implement any pathfinding algorithm. Read the map with your eyes and brain, not with code.

  **⚠️ STEP COUNT RULE:** Write a coordinate trace and count arrows = steps. E.g. (1,0)→N→(1,1)→N→(1,2) = 2 arrows = steps:2. Do NOT confuse target coordinate with step count!

  **Multi-segment examples (THIS IS HOW YOU SHOULD USE IT):**
  \`\`\`
  // Trace an L-shaped corridor: north 4 cells, then turn east 3 cells
  movePath([{dir: "north", steps: 4}, {dir: "east", steps: 3}])

  // Trace a zigzag: east 2, south 3, east 1, south 2
  movePath([{dir: "east", steps: 2}, {dir: "south", steps: 3}, {dir: "east", steps: 1}, {dir: "south", steps: 2}])

  // Trace a long winding path through visible corridors
  movePath([{dir: "north", steps: 3}, {dir: "east", steps: 1}, {dir: "north", steps: 2}, {dir: "west", steps: 4}, {dir: "south", steps: 1}])
  \`\`\`

  **❌ BAD — single segment (wastes a screenshot cycle):**
  \`\`\`
  movePath([{dir: "north", steps: 4}])   // You can see the turn! Why stop here?
  \`\`\`

- **\`getPlayerStatus()\`** — Get the player's current status.
  - Returns: \`{ success, position, northDistance, southDistance, eastDistance, westDistance, reachedGoal, message }\`
  - Obstacle distances are in **grid cells** in each of the 4 compass directions.
  - A distance < 0.7 cells means there is a wall immediately blocking that direction.
  - A distance ≥ 1.0 cells means the path is open for at least 1 cell.
  - Count the green grid lines in the screenshot to verify distances.
  - **⚠️ Distances are how many cells you CAN move**, so use them directly as \`steps\`. E.g. eastDistance=3.6 → \`{dir:"east", steps:3}\`.
  - **Common mistake**: If you are at position x=1 and want to reach x=3, you need \`steps: 2\` (= 3−1), NOT \`steps: 3\`. The \`steps\` value is how many cells to CROSS, not a target coordinate.

**Direction mapping on screen (top-down view):**
- North (+Z) = up on screen
- South (-Z) = down on screen
- East (+X) = right on screen
- West (-X) = left on screen

**Grid system**: The maze floor has green grid lines showing cell boundaries. Each cell is a square. The player always starts at a cell center and moves to another cell center. **Count the number of grid lines you need to CROSS** (not the target grid line number) to determine \`steps\`.

- **\`getMazeMap()\`** — Get a complete ASCII representation of the entire maze.
  - Returns: \`{ success, width, height, playerCell, goalCell, map, message }\`
  - The \`map\` field is an ASCII art string showing the full maze layout:
    - \`P\` = Player position, \`G\` = Goal position
    - \`+\` = corner, \`---\` = horizontal wall, \`|\` = vertical wall
    - Spaces between walls = open passages
    - Rows are labeled y0, y1, ... from bottom (south) to top (north)
    - Columns are labeled x0, x1, ... from left (west) to right (east)
  - **Use this to plan the route from P to G!**
  - Example output (4×4 maze):
    \`\`\`
         x0  x1  x2  x3
        +---+---+---+---+
     y3 | P |           |
        +   +---+   +   +
     y2 |       |   |   |
        +---+   +   +   +
     y1 |   |       |   |
        +   +   +---+   +
     y0 |           | G |
        +---+---+---+---+
    \`\`\`

**Workflow**:
1. Call \`getMazeMap()\` to get the full maze layout.
2. Plan the route from P to G by reading the ASCII map. Write a brief coordinate trace to verify step counts.
3. Execute with \`movePath()\`. If blocked or not at G yet, re-plan from current position.
4. Use \`getPlayerStatus()\` only if you need to verify your current position.
`.trim();

// ---- Function implementations ----

interface MoveSequenceResult {
    success: boolean;
    stepsRequested: number;
    stepsCompleted: number;
    blocked: boolean;
    reachedGoal: boolean;
    totalDistanceMoved: number;
    position: string;
    message: string;
}

interface PlayerStatusResult {
    success: boolean;
    position: string;
    northDistance: number;
    southDistance: number;
    eastDistance: number;
    westDistance: number;
    reachedGoal: boolean;
    message: string;
}

type Direction = "north" | "south" | "east" | "west";

interface PathSegment {
    dir: Direction;
    steps: number;
}

/**
 * Move the player along a multi-segment path.
 * Each segment specifies a direction and distance (in meters).
 * Executes each segment sequentially, stopping early if blocked or goal reached.
 * @param segments Array of {dir, steps} objects, e.g. [{dir:"east", steps:3}, {dir:"north", steps:2}]
 */
export async function movePath(segments: PathSegment[]): Promise<MoveSequenceResult> {
    if (!Array.isArray(segments) || segments.length === 0) {
        throw new Error(
            `movePath: 'segments' must be a non-empty array of {dir, steps} objects (got ${JSON.stringify(segments)}). Read module.description for usage.`
        );
    }
    if (segments.length > 20) {
        throw new Error(
            `movePath: Too many segments (max 20, got ${segments.length}). Plan shorter paths and re-observe.`
        );
    }
    const validDirections = ["north", "south", "east", "west"];
    const directions: string[] = [];
    const distances: number[] = [];

    for (let i = 0; i < segments.length; i++) {
        const seg = segments[i];
        if (!seg || typeof seg !== 'object' || !seg.dir || typeof seg.steps !== 'number') {
            throw new Error(
                `movePath: Invalid segment at index ${i}: ${JSON.stringify(seg)}. Each must be {dir: string, steps: number}.`
            );
        }
        if (!validDirections.includes(seg.dir)) {
            throw new Error(
                `movePath: Invalid direction '${seg.dir}' at index ${i}. Must be one of ${JSON.stringify(validDirections)}.`
            );
        }
        if (seg.steps < 1 || seg.steps > 10 || !Number.isInteger(seg.steps)) {
            throw new Error(
                `movePath: 'steps' at index ${i} must be an integer between 1 and 10 (got ${seg.steps}). Steps are in grid cells.`
            );
        }
        directions.push(seg.dir);
        distances.push(seg.steps);
    }

    // Print a visual route log
    const dirArrow: Record<string, string> = { north: "↑N", south: "↓S", east: "→E", west: "←W" };
    const totalSteps = distances.reduce((a, b) => a + b, 0);
    const routeStr = segments.map(s => `${dirArrow[s.dir]}×${s.steps}`).join(" → ");
    console.log(`movePath: ${routeStr}  (${segments.length} segments, ${totalSteps} total steps)`);

    const directionsJson = JSON.stringify(directions);
    const distancesJson = JSON.stringify(distances);
    const resultJson = await new Promise<string>((resolve, reject) => {
        try {
            CS.LLMAgent.MazePlayerBridge.MoveSequenceV2(directionsJson, distancesJson, (json: string) => resolve(json));
        } catch (error: any) {
            reject(error);
        }
    });

    return JSON.parse(resultJson) as MoveSequenceResult;
}

/**
 * Get the player's current status including position and obstacle distances in all 4 directions.
 */
export async function getPlayerStatus(): Promise<PlayerStatusResult> {
    const resultJson = await new Promise<string>((resolve, reject) => {
        try {
            CS.LLMAgent.MazePlayerBridge.GetPlayerStatus((json: string) => resolve(json));
        } catch (error: any) {
            reject(error);
        }
    });

    return JSON.parse(resultJson) as PlayerStatusResult;
}

interface MazeMapResult {
    success: boolean;
    width: number;
    height: number;
    playerCell: string;
    goalCell: string;
    map: string;
    message: string;
}

/**
 * Get a complete ASCII representation of the entire maze layout.
 * Shows all walls, the player position (P), and the goal (G).
 * Use this to plan the complete route before moving.
 */
export async function getMazeMap(): Promise<MazeMapResult> {
    const resultJson = await new Promise<string>((resolve, reject) => {
        try {
            CS.LLMAgent.MazePlayerBridge.GetMazeMap((json: string) => resolve(json));
        } catch (error: any) {
            reject(error);
        }
    });

    console.log(`getMazeMap: ${resultJson}`);

    return JSON.parse(resultJson) as MazeMapResult;
}
