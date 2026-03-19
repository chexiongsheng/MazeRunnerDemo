/**
 * Builtin: Maze Player Control Functions
 *
 * Provides helper functions to control the player character in a maze scene.
 * Uses absolute compass directions (north/south/east/west) for movement.
 * Camera is top-down overhead, so directions on screen are constant.
 * All functions interact with the C# MazePlayerBridge via PuerTS.
 */

// ---- Summary for tool description (always in context) ----

export const summary = `**maze-control** — Control the player in the maze. \`movePath([{dir, steps}, ...])\` executes a **multi-segment path** in one call (e.g. north 3 → east 2 → south 4 = 3 segments). \`getPlayerStatus()\` returns position and obstacle distances in grid cells. **Always plan the longest visible path with multiple segments per call — do NOT call movePath with only 1 segment unless at a dead end.** Read \`.description\` for details.`;

// ---- Description for on-demand access via import ----

export const description = `
- **\`movePath(segments)\`** — Move the player along a **multi-segment planned path** in a single call. Steps are in **grid cells** (each cell = 2m on the ground, shown by green grid lines).
  - \`segments\` (array, required): Array of objects \`{ dir: string, steps: number }\`.
    - \`dir\`: compass direction — "north", "south", "east", or "west"
    - \`steps\`: number of grid cells **to move** (relative displacement, NOT an absolute coordinate!) (1–10, integer). E.g. if you are at column 1 and want to reach column 3, steps = 3 − 1 = **2**, not 3.
  - **You SHOULD include 2–6 segments per call** — trace the full visible corridor through all its turns.
  - The player always lands exactly at a cell center (snaps to grid).
  - Max 20 segments per call.
  - Returns: \`{ success, stepsRequested, stepsCompleted, blocked, reachedGoal, totalDistanceMoved, position, message }\`
  - Executes each segment sequentially. Stops early if blocked by a wall or if the goal is reached.

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

**Workflow**: Call \`getPlayerStatus()\` ONCE to sense immediate surroundings, take ONE screenshot to visually trace the corridor as far as you can see through ALL visible turns, then plan the ENTIRE visible path as a multi-segment \`movePath()\` call. Do NOT stop at the first corner — keep tracing.
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
