import { Config } from "@remotion/cli/config";

Config.setVideoImageFormat("jpeg");
Config.setJpegQuality(92);
Config.setCodec("h264");
Config.setCrf(18);
Config.setOverwriteOutput(true);
// Bound memory on the development PC while other art tools are open.
Config.setConcurrency(1);
Config.setOffthreadVideoCacheSizeInBytes(512 * 1024 * 1024);
