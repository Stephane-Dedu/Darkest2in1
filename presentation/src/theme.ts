import { loadFont as loadAlegreya } from "@remotion/google-fonts/AlegreyaSans";
import { loadFont as loadFell } from "@remotion/google-fonts/IMFellEnglishSC";
import { loadFont as loadGrenze } from "@remotion/google-fonts/GrenzeGotisch";

// Grenze Gotisch for titles, IM Fell for small caps, Alegreya Sans (DD2's own body face) for text.
export const fonts = {
  title: loadGrenze("normal", { weights: ["600", "700"], subsets: ["latin"] }).fontFamily,
  caps: loadFell("normal", { subsets: ["latin"] }).fontFamily,
  body: loadAlegreya("normal", { weights: ["400", "500", "700"], subsets: ["latin"] }).fontFamily,
};

export const colors = {
  ink: "#0a0807",
  bone: "#e2d5bb",
  ash: "#9a8d78",
  torch: "#e8912f",
  ember: "#ffb35c",
  blood: "#9b2116",
  gold: "#c9a35a",
};

export const FPS = 30;
export const WIDTH = 1920;
export const HEIGHT = 1080;
