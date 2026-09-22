"""Thunderstore icon: 256x256 PNG.

The number is not drawn by hand. It is lifted out of `docs/images/icon-source.png`, a crop of a
real screenshot of the mod running, by masking the glyph faces and upscaling the MASK rather than
the pixels, which keeps the edges clean the way an SDF does. So the icon carries the game's own
letterforms. The outline, gradient and plate are added here. No PIL on the build box, hence the
hand-rolled PNG writer.
"""
import os
import struct
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "..", "docs", "images", "icon-source.png")
DST = os.path.join(HERE, "..", "thunderstore", "icon.png")

S = 256
# Window around the "312" in that crop, with its neighbours excluded.
X0, X1, Y0, Y1 = 10, 110, 8, 76


def read_png(path):
    d = open(path, "rb").read()
    pos, idat, ct = 8, b"", 6
    while pos < len(d):
        ln = struct.unpack(">I", d[pos:pos + 4])[0]
        tag, data = d[pos + 4:pos + 8], d[pos + 8:pos + 8 + ln]
        if tag == b"IHDR":
            w, h, _bd, ct = struct.unpack(">IIBB", data[:10])
        elif tag == b"IDAT":
            idat += data
        pos += 12 + ln
    raw = zlib.decompress(idat)
    bpp = 4 if ct == 6 else 3
    stride = w * bpp
    out, prev, i = bytearray(), bytearray(stride), 0
    for _y in range(h):
        f = raw[i]; i += 1
        line = bytearray(raw[i:i + stride]); i += stride
        if f:
            for x in range(stride):
                a = line[x - bpp] if x >= bpp else 0
                b = prev[x]
                c = prev[x - bpp] if x >= bpp else 0
                if f == 1: line[x] = (line[x] + a) & 255
                elif f == 2: line[x] = (line[x] + b) & 255
                elif f == 3: line[x] = (line[x] + (a + b) // 2) & 255
                else:
                    p = a + b - c
                    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                    pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                    line[x] = (line[x] + pr) & 255
        out += line
        prev = line
    return w, h, bpp, bytes(out)


def blur(m, hh, ww, passes=1):
    for _ in range(passes):
        out = [[0.0] * ww for _ in range(hh)]
        for y in range(hh):
            for x in range(ww):
                s = n = 0.0
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        yy, xx = y + dy, x + dx
                        if 0 <= yy < hh and 0 <= xx < ww:
                            s += m[yy][xx]; n += 1
                out[y][x] = s / n
        m = out
    return m


def dilate(m, hh, ww, passes):
    for _ in range(passes):
        out = [[0.0] * ww for _ in range(hh)]
        for y in range(hh):
            row = out[y]
            for x in range(ww):
                v = m[y][x]
                for dy in (-1, 0, 1):
                    yy = y + dy
                    if 0 <= yy < hh:
                        mr = m[yy]
                        for dx in (-1, 0, 1):
                            xx = x + dx
                            if 0 <= xx < ww and mr[xx] > v:
                                v = mr[xx]
                row[x] = v
        m = out
    return m


# ---- 1. the glyph mask, straight out of the screenshot
w, h, bpp, px = read_png(SRC)
cw, ch = X1 - X0, Y1 - Y0
face = [[0.0] * cw for _ in range(ch)]
for y in range(ch):
    for x in range(cw):
        i = ((Y0 + y) * w + (X0 + x)) * bpp
        r, g, b = px[i], px[i + 1], px[i + 2]
        # The face is pure white; grass and sea are neither that bright nor that neutral.
        face[y][x] = 1.0 if (max(r, g, b) - min(r, g, b)) <= 22 and min(r, g, b) >= 205 else 0.0

xs = [x for y in range(ch) for x in range(cw) if face[y][x] > 0]
ys = [y for y in range(ch) for x in range(cw) if face[y][x] > 0]
gx0, gx1, gy0, gy1 = min(xs), max(xs), min(ys), max(ys)
face = blur(face, ch, cw, 1)

# ---- 2. place it in the icon, scaled to fit with a margin
gw, gh = gx1 - gx0 + 1, gy1 - gy0 + 1
target_w = S * 0.80
scale = min(target_w / gw, (S * 0.62) / gh)
ox, oy = (S - gw * scale) / 2.0, (S - gh * scale) / 2.0

mask = [[0.0] * S for _ in range(S)]
for y in range(S):
    for x in range(S):
        fx = (x - ox) / scale + gx0
        fy = (y - oy) / scale + gy0
        if 0 <= fx < cw - 1 and 0 <= fy < ch - 1:
            x0, y0 = int(fx), int(fy)
            tx, ty = fx - x0, fy - y0
            a = face[y0][x0] * (1 - tx) + face[y0][x0 + 1] * tx
            b = face[y0 + 1][x0] * (1 - tx) + face[y0 + 1][x0 + 1] * tx
            v = a * (1 - ty) + b * ty
            mask[y][x] = 0.0 if v < 0.42 else (1.0 if v > 0.58 else (v - 0.42) / 0.16)

outline = blur(dilate(mask, S, S, 5), S, S, 1)

# ---- 3. paint
DARK = (22, 16, 12)
OUTLINE = (10, 7, 5)
GOLD_TOP = (255, 214, 120)
GOLD_BOT = (243, 156, 28)

img = bytearray(S * S * 4)


def put(x, y, rgb, a):
    if a <= 0:
        return
    i = (y * S + x) * 4
    ia = 1 - a
    img[i] = int(rgb[0] * a + img[i] * ia)
    img[i + 1] = int(rgb[1] * a + img[i + 1] * ia)
    img[i + 2] = int(rgb[2] * a + img[i + 2] * ia)
    img[i + 3] = int(min(255, 255 * a + img[i + 3] * ia))


rad = 44
for y in range(S):
    for x in range(S):
        dx = max(rad - x, 0, x - (S - 1 - rad))
        dy = max(rad - y, 0, y - (S - 1 - rad))
        if dx * dx + dy * dy <= rad * rad:
            put(x, y, DARK, 1.0)
            d = (((x - S / 2) ** 2 + (y - S / 2) ** 2) ** 0.5) / (S * 0.62)
            if d < 1:
                put(x, y, (132, 90, 36), 0.45 * (1 - d) ** 2)

for y in range(S):
    for x in range(S):
        if outline[y][x] > 0:
            put(x, y, OUTLINE, outline[y][x])
for y in range(S):
    t = min(max((y - oy) / max(gh * scale, 1), 0.0), 1.0)
    rgb = tuple(int(GOLD_TOP[k] + (GOLD_BOT[k] - GOLD_TOP[k]) * t) for k in range(3))
    for x in range(S):
        if mask[y][x] > 0:
            put(x, y, rgb, mask[y][x])

out = bytearray()
for y in range(S):
    out.append(0)
    out += img[y * S * 4:(y + 1) * S * 4]


def chunk(tag, data):
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)


open(DST, "wb").write(b"\x89PNG\r\n\x1a\n"
                      + chunk(b"IHDR", struct.pack(">IIBBBBB", S, S, 8, 6, 0, 0, 0))
                      + chunk(b"IDAT", zlib.compress(bytes(out), 9))
                      + chunk(b"IEND", b""))
print(DST, "glyph", gw, "x", gh, "scaled", round(scale, 2))
