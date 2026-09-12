# Idle Clinic art

These are original, local Blender assets for the fixed clinic. They do not reuse the former train scene or an image backdrop.

Source: `assets/idle-clinic-game.blend`. Regenerate furniture and all character clips with:

```sh
/Applications/Blender.app/Contents/MacOS/Blender -b --python Tools/create_clinic_assets.py
```

Regenerate the 1024px opaque nurse-and-care-bag icon without touching FBXs:

```sh
/Applications/Blender.app/Contents/MacOS/Blender -b --python Tools/create_clinic_assets.py -- --icon-only
```

`Patient`, `Receptionist`, and `Nurse` each contain one skinned multimat mesh, an 18-bone skeleton, and six baked actions: `Idle`, `Walk`, `CheckIn`, `Treat`, `Sit`, and `Call`. The generator validates every joint against the authored pose before export. The scoped Unity importer uses Legacy clips; the pooled presentation layer samples them using simulation time. Reduced Motion freezes cyclic poses while preserving essential patient and staff travel.

Furniture contains authored empty sockets named `Asset__patient`, `Asset__staff`, and `ReceptionDesk__cash`. The renderer reads these imported transforms for occupancy and payment origins. The asset conversion applies the same coordinate transform to geometry, skeletons, and sockets. Do not correct an apparent mirror by moving actors independently of their furniture; inspect imported transforms and geometry first.

`AssetsPreview.png` and `CarePosePreview.png` are Blender source-art previews, not Unity or device runtime screenshots. `IconProof-before-tonal-pass.png` preserves an intermediate render. The final shipped icon is `AppIcon.png`.

The nine FBXs total approximately 3.1 MB. They use material colors rather than downloaded textures. The world shares materials and primitive meshes and pools skinned actors. No Blender process used for these assets operates on the user's open GUI scene.
