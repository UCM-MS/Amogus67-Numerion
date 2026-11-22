"""Generate reproducible, seamless 4096px PBR textures; no downloaded asset licences.

Requires numpy + Pillow. Normal gradients wrap at tile edges. HDRP mask channels:
metallic, ambient occlusion, detail mask, smoothness. sRGB applies to albedo only.
"""
from pathlib import Path
import argparse
import numpy as np
from PIL import Image


def generate(size=4096):
    root = Path(__file__).resolve().parents[1] / 'Assets/Lumen/Resources/Textures'
    root.mkdir(parents=True, exist_ok=True)
    axis = np.arange(size, dtype=np.float32) / size
    x, y = axis[None, :], axis[:, None]
    tau = np.float32(2 * np.pi)
    # Integer wave frequencies make the material tile seamlessly in both axes.
    grain = (np.sin(tau * (251*x+179*y)) + np.cos(tau*(97*x-139*y))
             + np.sin(tau*(487*x+431*y))) / 3
    broad = (np.sin(tau*(3*x+5*y)) + np.cos(tau*(7*x-4*y))) / 2
    surfaces = {
        'grass': ([118, 157, 102], .045, .18, 0, .14),
        'stone': ([216, 201, 173], .07, .12, 0, .28),
        'wood': ([164, 121, 83], .055, .16, 0, .31),
        'metal': ([170, 190, 198], .018, .08, .92, .67),
    }
    for name, (base, strength, variation, metallic, smoothness) in surfaces.items():
        if name == 'wood':
            height = .5 + .25*np.sin(tau*(18*x+.22*np.sin(tau*3*y))) + .07*grain
        elif name == 'metal':
            height = .5 + .03*np.sin(tau*1400*y) + .01*grain + .015*broad
        elif name == 'stone':
            joints = np.minimum(np.abs(np.sin(tau*4*x)), np.abs(np.sin(tau*4*y)))
            height = .5 + .14*broad + .08*grain - .25*np.exp(-joints*120)
        else:
            height = .5 + .15*broad + .12*grain + .06*np.sin(tau*(731*x+13*y))
        height = np.broadcast_to(height, (size, size)).astype(np.float32)
        albedo = np.clip(np.asarray(base, dtype=np.float32)[None, None, :] *
                         (1 + variation*(height[..., None]-.5)), 0, 255).astype(np.uint8)
        Image.fromarray(albedo).save(root / f'{name}-albedo.png', compress_level=6)
        del albedo
        dx = (np.roll(height, 1, 1) - np.roll(height, -1, 1)) * strength * 32
        dy = (np.roll(height, 1, 0) - np.roll(height, -1, 0)) * strength * 32
        normal = np.stack((dx, dy, np.ones_like(height)), axis=-1)
        normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
        Image.fromarray(np.clip((normal*.5+.5)*255, 0, 255).astype(np.uint8)).save(
            root / f'{name}-normal.png', compress_level=6)
        del dx, dy, normal
        mask = np.empty((size, size, 4), dtype=np.uint8)
        mask[..., 0] = round(metallic*255)
        mask[..., 1] = np.clip(225 + height*30, 0, 255).astype(np.uint8)
        mask[..., 2] = 255
        mask[..., 3] = np.clip((smoothness + .04*(height-.5))*255, 0, 255).astype(np.uint8)
        Image.fromarray(mask).save(root / f'{name}-mask.png', compress_level=6)
        print(f'{name}: {size}×{size} albedo / normal / HDRP mask', flush=True)
        del height, mask


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--size', type=int, default=4096, choices=[1024, 2048, 4096])
    generate(parser.parse_args().size)
