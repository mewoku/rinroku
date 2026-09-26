import { Config } from "@remotion/cli/config";

Config.setVideoImageFormat("jpeg");
Config.setOverwriteOutput(true);
// Low-RAM friendly defaults; override with --concurrency on a bigger machine.
Config.setConcurrency(1);
Config.setEntryPoint("./src/index.ts");
