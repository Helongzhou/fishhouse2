# Unity 6 candidate technical proxies

These three GLB files are the same bytes as the Unity 2022 copies. They are generic stand-ins, not ember tetra, neon tetra, or finished plants. L1/L2 are not imported here. Glass and water are not replaced.

`EnableTechnicalProxyVisuals` defaults off. The player scene does not set it. Clearing it keeps the original placeholders.

`TechProxyValidation.unity` is for a person to look at. It is not in the player build list, it does not save, and a BatchMode run does not prove the picture.

glTFast is pinned to `com.unity.cloud.gltfast` 6.16.1. Its package manifest requires Unity 2022.3.67f2 or newer, and its changelog names Unity 6.0 and Unity 6.3. URP import, including mask and double-sided materials, is documented for the Universal Render Pipeline. This pin has not been imported or compiled in the cloud environment, so compatibility with Unity 6000.6.8f1 and URP 17.6.0 is not a measured pass.

`-projectPath` does not isolate `Application.persistentDataPath` (`~/Library/Application Support/Aquarium Journal/Aquarium Journal` for this product name). The candidate tests refuse that location and only write a save under a brand-new directory passed as `-aquariumSaveRoot`. See `RUN-MAC.md`.
