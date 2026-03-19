/**
 * Builtin: Screenshot Functions (Maze Runner variant)
 *
 * Provides helper functions to capture Unity game view screenshots.
 * For the maze runner agent, only the game view capture is needed (not scene view).
 * The returned object includes a special `__image` marker that the eval tool runner
 * detects and converts into multi-modal image content so the LLM can visually
 * analyze the screenshot.
 */

// ---- Summary for tool description (always in context) ----

export const summary = `**screenshot** — Capture a screenshot of the current game view for visual analysis of the maze. Read \`.description\` to see available functions and their signatures.`;

// ---- Description for on-demand access via import ----

export const description = `
- **\`captureScreenshot(maxWidth?, maxHeight?)\`** — Capture the current game view (what the player sees in the maze).
  - \`maxWidth\` (number, default 512): Maximum width in pixels (64-1920).
  - \`maxHeight\` (number, default 512): Maximum height in pixels (64-1080).
  - Returns a result object. On success the image is automatically sent to you for visual analysis.

**Note**: The captured image is automatically included as visual content in the tool response.
You do NOT need to process the base64 data yourself — just call the function and you will see the screenshot.
Use this to observe the maze: identify walls, corridors, turns, dead ends, and the goal marker.
`.trim();

// ---- Function implementations ----

interface ScreenshotResult {
    success: boolean;
    message: string;
    /** Special marker for the eval tool runner to detect image content. */
    __image?: {
        base64: string;
        mediaType: string;
    };
}

/**
 * Capture the Unity Game view screenshot.
 * @param maxWidth Maximum width in pixels (default 512, range 64-1920)
 * @param maxHeight Maximum height in pixels (default 512, range 64-1080)
 */
export async function captureScreenshot(maxWidth: number = 512, maxHeight: number = 512): Promise<ScreenshotResult> {
    validateDimensions(maxWidth, maxHeight, 'captureScreenshot');

    const resultJson = await new Promise<string>((resolve, reject) => {
        try {
            CS.LLMAgent.ScreenCaptureBridge.CaptureScreenAsync(
                maxWidth,
                maxHeight,
                (json: string) => resolve(json)
            );
        } catch (error: any) {
            reject(error);
        }
    });

    const result = JSON.parse(resultJson);

    if (!result.success) {
        return {
            success: false,
            message: `Screenshot capture failed: ${result.error || 'Unknown error'}`,
        };
    }

    return {
        success: true,
        message: `Game view screenshot captured successfully (${result.width}x${result.height}).`,
        __image: {
            base64: result.base64,
            mediaType: 'image/png',
        },
    };
}

// ---- Internal helpers ----

function validateDimensions(maxWidth: number, maxHeight: number, funcName: string): void {
    if (typeof maxWidth !== 'number' || !Number.isInteger(maxWidth) || maxWidth < 64 || maxWidth > 1920) {
        throw new Error(`${funcName}: 'maxWidth' must be an integer between 64 and 1920 (got ${JSON.stringify(maxWidth)}). Read module.description for usage.`);
    }
    if (typeof maxHeight !== 'number' || !Number.isInteger(maxHeight) || maxHeight < 64 || maxHeight > 1080) {
        throw new Error(`${funcName}: 'maxHeight' must be an integer between 64 and 1080 (got ${JSON.stringify(maxHeight)}). Read module.description for usage.`);
    }
}
