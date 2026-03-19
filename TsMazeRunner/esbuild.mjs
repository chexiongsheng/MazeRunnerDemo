import { build } from 'esbuild';
import { readdirSync, mkdirSync, existsSync } from 'fs';
import { join } from 'path';

// Build maze-runner builtins modules
const builtinSrcDir = 'src/builtins';
const builtinOutDir = '../Assets/Resources/maze-runner/builtins';

const builtinFiles = existsSync(builtinSrcDir)
    ? readdirSync(builtinSrcDir).filter(f =>
        f.endsWith('.mts') && !f.endsWith('.d.mts'))
    : [];

if (builtinFiles.length > 0) {
    if (!existsSync(builtinOutDir)) {
        mkdirSync(builtinOutDir, { recursive: true });
    }

    const builtinEntries = builtinFiles.map(f => join(builtinSrcDir, f));

    await build({
        entryPoints: builtinEntries,
        bundle: true,
        format: 'esm',
        outdir: builtinOutDir,
        outExtension: { '.js': '.mjs' },
        platform: 'neutral',
        target: 'esnext',
        sourcemap: false,
        minify: false,
        keepNames: true,
        external: [],
        define: {
            'process.env.NODE_ENV': '"production"',
        },
    });

    console.log(`[esbuild:maze-runner] Built ${builtinFiles.length} builtins module(s) → ${builtinOutDir}/`);
} else {
    console.log('[esbuild:maze-runner] No builtins modules found in src/builtins/');
}
