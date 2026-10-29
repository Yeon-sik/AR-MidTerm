"""Read the original photo; print reproducible pixel/plane measurements. Requires Pillow."""
import argparse
import hashlib
import json
from statistics import median
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument("image", nargs="?", default="iphone-t1.jpg")
args = parser.parse_args()
image = Image.open(args.image).convert("RGB")
if image.size != (1208, 2425):
    raise ValueError("Expected the recorded, unscaled original: 1208 x 2425")
pixels = image.load()


def black(x, y):
    r, g, b = pixels[x, y]
    return r < 125 and g < 100 and b < 100


def bounds(roi, predicate):
    left, top, right, bottom = roi
    points = [(x, y) for y in range(top, bottom) for x in range(left, right) if predicate(x, y)]
    return [min(x for x, y in points), min(y for x, y in points),
            max(x for x, y in points) + 1, max(y for x, y in points) + 1]


spec = bounds((20, 2128, 800, 2215), lambda x, y: sum(pixels[x, y]) < 3 * 175)
box = bounds((860, 40, 1180, 180), black)
top_samples = []
for x in range(1009, 1026):
    dark = [y for y in range(45, 90) if black(x, y)]
    if dark:
        top_samples.append(min(dark))
scale = .058 / image.width
anchors = {"serving-price": (spec[0] - 6, (spec[1] + spec[3]) / 2),
           "observed-sellers": ((box[0] + box[2]) / 2, median(top_samples))}
with open(args.image, "rb") as source:
    digest = hashlib.sha256(source.read()).hexdigest()
print(json.dumps({"sourceSha256": digest, "imagePixels": image.size, "metresPerPixel": scale,
                  "originPixels": [604, 1212.5], "positiveZ": "image down",
                  "photoDerivedHeightMetres": image.height * scale, "specHalfOpenBounds": spec,
                  "boxHalfOpenBounds": box, "boxTopSamples": top_samples,
                  "anchors": {name: {"pixel": [x, y], "xMetres": (x - 604) * scale,
                                     "zMetres": (y - 1212.5) * scale} for name, (x, y) in anchors.items()}}, indent=2))
