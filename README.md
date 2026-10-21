# Vertigo Wheel Demo

A Unity wheel-based risk/reward case study using the supplied Vertigo Games assets.

## Run

Open the project in **Unity 6000.3.23f1**, open `Assets/_Game/Scenes/Game.unity`, and press Play. Android builds use landscape orientation, ARM64 and IL2CPP; install Unity's Android Build Support with SDK/NDK and OpenJDK to build an APK.

## Gameplay and cash-out assumption

Spin to collect rewards. A bomb loses the run's unbanked rewards. Continue acknowledges a reward and advances to the next zone; cash-out and bomb results offer Restart. Runs are kept in memory only.

The brief states:

> Player can choose to leave when wheel is not spinning and when the zone is safe or at the super zone.

This is interpreted as **`SafeAndSuperZonesOnly`**: Exit is available in the `Ready` state on the **currently displayed** safe wheel (every 5 zones) or super wheel (every 30). Super takes priority when both coincide. Cash-out is therefore possible before spinning that wheel. After Continue advances the zone, eligibility follows the new displayed wheel. Exit opens a confirmation before collecting rewards.

The alternative `AnyZone` policy is available in `Assets/_Game/Settings/Zones/ZoneSettings.asset`.

## Structure and tuning

- `Scripts/Core`: Unity-independent rules, weighted selection and run settlement.
- `Scripts/Settings`: validated ScriptableObjects for rewards, pools, wheels, zones and animations.
- `Scripts/UI`: reusable TMP/uGUI views and DOTween motion.
- `Scripts/App`: scene composition and flow coordination.

All paths above are under `Assets/_Game`. Tune assets in `Assets/_Game/Settings`; the default wheels share a reward pool with bronze/silver/golden quantity multipliers of 1x/2x/10x. SpriteAtlas V2 assets are manually editable under `Assets/_Game/Art/UI/Atlases`.

## Tests

Use **Window > General > Test Runner** to run EditMode and PlayMode tests. They cover rules, distributions, flow/interruption handling and UI layouts at 20:9, 16:9 and 4:3.
