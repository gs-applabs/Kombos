# Draws the installer wizard pictures (light + dark) from kombos.ico into installer\wizard\.
#   python tools\make-wizard-art.py        (needs Pillow)
# Tall picture: 202x386 / 269x515 / 336x643 / 430x824 (100 / 125 / 150 / 200 %), light and dark versions.
# Small picture: PNG with a TRANSPARENT background (no white square on a dark page): 55x58 / 64x68 / 83x80 / 110x106.
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'installer', 'wizard')
os.makedirs(OUT, exist_ok=True)

ico = Image.open(os.path.join(ROOT, 'kombos.ico'))
ico.size = max(ico.info['sizes'])
ICON = ico.convert('RGBA')


def font(size):
    for f in (r'C:\Windows\Fonts\bahnschrift.ttf', r'C:\Windows\Fonts\segoeuib.ttf', r'C:\Windows\Fonts\arialbd.ttf'):
        if os.path.exists(f):
            return ImageFont.truetype(f, size)
    return ImageFont.load_default()


THEMES = {
    'light': dict(bg=(251, 248, 243), ink=(38, 35, 31), soft=(110, 104, 96), band=(193, 99, 58)),
    'dark': dict(bg=(27, 23, 19), ink=(244, 239, 232), soft=(170, 160, 148), band=(193, 99, 58)),
}


def tall(w, h, theme):
    t = THEMES[theme]
    im = Image.new('RGB', (w, h), t['bg'])
    d = ImageDraw.Draw(im)
    d.rectangle([0, int(h * 0.86), w, h], fill=t['band'])
    size = int(w * 0.62)
    ic = ICON.resize((size, size), Image.LANCZOS)
    im.paste(ic, ((w - size) // 2, int(h * 0.12)), ic)
    f = font(int(w * 0.17))
    tw = d.textlength('Kombos', font=f)
    d.text(((w - tw) / 2, h * 0.50), 'Kombos', font=f, fill=t['ink'])
    f2 = font(int(w * 0.075))
    tw = d.textlength('Claude + AI tools', font=f2)
    d.text(((w - tw) / 2, h * 0.50 + w * 0.22), 'Claude + AI tools', font=f2, fill=t['soft'])
    return im


def small(w, h):
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    size = int(min(w, h) * 0.92)
    ic = ICON.resize((size, size), Image.LANCZOS)
    im.paste(ic, ((w - size) // 2, (h - size) // 2), ic)
    return im


for (w, h, tag) in ((202, 386, '100'), (269, 515, '125'), (336, 643, '150'), (430, 824, '200')):
    tall(w, h, 'light').save(os.path.join(OUT, f'large-{tag}.bmp'))
    tall(w, h, 'dark').save(os.path.join(OUT, f'dark-large-{tag}.bmp'))
for (w, h, tag) in ((55, 58, '100'), (64, 68, '125'), (83, 80, '150'), (110, 106, '200')):
    small(w, h).save(os.path.join(OUT, f'small-{tag}.png'))
print('wizard art written to', OUT)
