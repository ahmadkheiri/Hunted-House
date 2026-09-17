MANSION STALKER
Scene: PersianHouse_ChildHorror.unity
Select Mansion stalker in the Hierarchy to edit public settings on MansionStalker.
The live Inspector shows state, chase-recognition range, visual contact, and last known position during Play Mode.

BEHAVIOR
Roaming: weighted random observation stops with recent-stop avoidance. Room stops are less frequent than galleries and overlooks. Pauses and scan sweeps vary; it does not visit every room in order.
Alert: investigate a visible player, a directly visible lantern, or an estimated visible lantern-lit wall/floor patch. A patch attracts attention to that surface, not to the hidden player's position.
Chase: requires unobstructed player visibility inside the brightness-dependent recognition range for recognitionSeconds.
Lost chase: after loseSightSeconds without recognition, investigate the stored last-seen location. Search nearby for searchDuration after arriving (or after investigationTravelTimeout), then resume roaming.
Walls block sight even within close range. Very close recognition works outside the usual forward view, but never through walls.
At contact the enemy stops near the child. This implementation covers stalking AI; damage, attacks, death, and hiding interactions are not implemented. Hiding here means breaking line of sight behind solid geometry.

DEFAULTS
roamingSpeed 1.15 m/s; alertSpeed 1.7 m/s; chaseSpeed 2.35 m/s.
playerDetectionDistance 20m; lanternDetectionDistance 28m; fieldOfView 110 degrees.
nearRecognitionDistance 2.5m (dark chase); dark alert range 5m; brightAlertDistance 18m (bright chase 9m).
fullBrightnessLumens 200; fullMoonBrightnessLux 0.3.
searchDuration 14s; searchRadius 5m; loseSightSeconds 1.4s; recognitionSeconds 0.45s.
Scan pauses 2.5..5.5 seconds, with occasional pauses after 9..16 seconds of walking.

LIGHT MODEL
Alert range = lerp(2 * nearRecognitionDistance, brightAlertDistance, brightness).
Chase range = 0.5 * alert range at every brightness level. Initial detection always enters Alert; minimumAlertSeconds (default 1.2s) gives time to move away or dim the lantern.
Brightness uses the larger of lantern lumens/fullBrightnessLumens and unobstructed moon lux/fullMoonBrightnessLux, clamped to 0..1.
Lantern off + blocked/disabled moon gives the minimum range. A 60-lumen lantern alone gives roughly 8.9m alert / 4.45m chase before flicker.
playerDetectionDistance also limits visual detection; use it at least as large as brightAlertDistance when tuning.
Light-spill detection samples surfaces and estimates inverse-square illumination with a surface-angle factor. It is a gameplay approximation, not a reading of rendered pixels or a full light-transport simulation.
All speeds, distances, light thresholds, recognition/search timings, scan pauses, and field of view are public Inspector fields.

NAVIGATION AND CHARACTER
46 reachable observation stops: 23 ground floor, 21 upper floor, 2 cellar.
Patrol markers are editable under Stalker navigation and patrol. Markers include rooms, courtyard/gallery overlooks, balconies, and stairs.
NavMesh built from actual mansion colliders; radius 0.32m, height 2.1m, step 0.42m.
NavMesh registration is owned by MansionNavigation. Do not remove Stalker navigation and patrol when replacing the model.
Replace the visual child objects on Mansion stalker while retaining MansionStalker, NavMeshAgent, CapsuleCollider, and an eyes transform.
MansionStalker.prefab is reusable; it automatically finds the player and scene patrol points if references are absent.
Tools > Persian House > Build or Rebuild Stalker and Navigation regenerates the enemy and navigation from the current mansion geometry. This resets the generated enemy and patrol markers to defaults; preserve custom tuning/models before using it.

VERIFICATION
STALKER_CHECKS.txt: state transitions, brightness relation, proximity, occlusion, memory, timed search, roaming.
STALKER_INTEGRATION_CHECKS.txt: actual player/lantern perception, wall occlusion, rear field-of-view, and actual upper-floor/cellar stair traversal.

STAIRS AND FOOTSTEPS
All three stair passages commit the enemy to a safe exit before it can scan, including during Alert and Chase state changes. Room and balcony stops follow ascent. Ground-floor courtyard lookouts follow descent; cellar arrivals use a same-floor observation stop because no courtyard is visible there. Stair markers are no longer patrol stops. Alert searches on stair treads are redirected to safe observation areas; LastKnownPosition still retains the actual sighting.
StalkerFootsteps plays four original generated boot impacts, paced by actual distance walked. It stops producing new steps while stationary. Audio is spatial, fades with distance, and is muffled by walls. Public settings: distancePerStep (0.75m), volume (0.7), audibleDistance (25m), and footstepClips for replacement audio.
STALKER_REFINEMENT_CHECKS.txt records the current threshold, stair, lookout, and footstep tests. Earlier check reports describe the previous iteration.
