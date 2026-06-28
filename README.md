# TexturePacker
Unity Texture Combiner. Pack multiple texture color channels into one texture!

## Installation

Install through Unity Package Manager with the Git URL:

```text
https://github.com/MythicFoundry/TexturePacker.git
```

For a pinned release, use a version tag:

```text
https://github.com/MythicFoundry/TexturePacker.git#v1.0.0
```

Open with **Tools/Texture Packer**

- Combine multiple textures into one output texture (For use in Mask maps or other packed texture techniques)
- Choose which channel each texture pulls from, and where it goes to
- Invert / multiply texture inputs for desired results
- Have default 0 - 1 values for unassigned texture inputs
- Save multiple presets to have different workflows in the same project
- Includes a default channel preset, an Unreal ORM preset (`R: Occlusion`, `G: Roughness`, `B: Metallic`), and an Unreal OSM preset (`R: Occlusion`, `G: Smoothness = 1 - Roughness`, `B: Metallic`)

![image](https://github.com/camobiwon/ChannelPacker/assets/30759426/a8347dae-1b64-4bb1-8049-3e608bb4200b)
![image](https://github.com/camobiwon/ChannelPacker/assets/30759426/7ba13073-1570-49ef-b1d5-5ea3b63cdf5e)

![All](https://github.com/camobiwon/ChannelPacker/assets/30759426/598f2ec7-9f20-4430-9742-eae82359be97)

TexturePacker is a heavily modified / rewritten version of [MaskPacker](https://www.reddit.com/r/Unity3D/comments/glkvp2/i_made_another_mask_map_packer_for_hdrp/).

Mask Packer was built for HDRP masks only, so I made this to be modular, flexible, and support many more workflows, while fixing some quirks / bugs I had with the original.

Thank you to the original creator of this, I appreciate it and hope others will find this to be useful :)
