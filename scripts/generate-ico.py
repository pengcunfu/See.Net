from PIL import Image
import os

src = os.path.join(os.path.dirname(__file__), '..', 'docs', 'logo.png')
dst = os.path.join(os.path.dirname(__file__), '..', 'packaging', 'assets', 'app.ico')

os.makedirs(os.path.dirname(dst), exist_ok=True)

img = Image.open(src).convert('RGBA')
sizes = [(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)]
img.save(dst, format='ICO', sizes=sizes)
print(f'Generated {dst} with sizes: {[s[0] for s in sizes]}')
print(f'File size: {os.path.getsize(dst)} bytes')
