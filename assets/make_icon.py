"""Generates Assets/app.ico: a dark cluster-map tile, drawn per size so small icons stay crisp."""
import os
from PIL import Image, ImageDraw

BG = (10, 15, 23, 255)
EDGE = (40, 56, 74, 255)
ALLOCATED = (34, 211, 238, 255)
ALLOCATED_DEEP = (12, 116, 138, 255)
FREE = (24, 36, 49, 255)

# Which of the 4x4 cells are allocated: a compacted block on the left, like a defragmented volume.
FILLED = {(0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1), (0, 2), (1, 2)}


def draw(size):
    scale = 4  # draw large, downscale once, so edges stay smooth at every size
    big = size * scale
    image = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    g = ImageDraw.Draw(image)

    radius = max(2, round(big * 0.16))
    g.rounded_rectangle((0, 0, big - 1, big - 1), radius=radius, fill=BG, outline=EDGE, width=max(1, big // 64))

    # Taskbar and title-bar sizes get a 2x2 grid: four cells read, sixteen turn to mush.
    cells = 2 if size <= 24 else 4
    filled = {(0, 0), (1, 0), (0, 1)} if cells == 2 else FILLED
    margin = round(big * (0.24 if cells == 2 else 0.19))
    gap = max(1, round(big * (0.06 if cells == 2 else 0.035)))
    grid = big - margin * 2
    cell = (grid - gap * (cells - 1)) // cells
    used = cell * cells + gap * (cells - 1)
    cell_span = cells
    left = (big - used) // 2
    top = (big - used) // 2

    for row in range(cell_span):
        for col in range(cell_span):
            x = left + col * (cell + gap)
            y = top + row * (cell + gap)
            colour = ALLOCATED if (col, row) in filled else FREE
            g.rounded_rectangle((x, y, x + cell - 1, y + cell - 1), radius=max(1, cell // 5), fill=colour)

    if cells == 4:
        # The last used cells read as "compacted into the block", the rest as free space.
        for col, row in ((2, 2), (0, 3)):
            x = left + col * (cell + gap)
            y = top + row * (cell + gap)
            g.rounded_rectangle((x, y, x + cell - 1, y + cell - 1), radius=max(1, cell // 5), fill=ALLOCATED_DEEP)

    return image.resize((size, size), Image.LANCZOS)


sizes = [16, 24, 32, 48, 64, 128, 256]
frames = [draw(size) for size in sizes]
os.makedirs("Assets", exist_ok=True)
frames[-1].save("Assets/app.ico", format="ICO", sizes=[(s, s) for s in sizes], append_images=frames[:-1])
print("wrote Assets/app.ico", os.path.getsize("Assets/app.ico"), "bytes")
frames[-1].save(os.environ["TEMP"] + "/icon-preview-256.png")
draw(32).resize((128, 128), Image.NEAREST).save(os.environ["TEMP"] + "/icon-preview-32.png")
print(os.environ["TEMP"])
