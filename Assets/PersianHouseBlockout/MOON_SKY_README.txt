MOONLIT NIGHT
Playable scene: PersianHouse_ChildHorror.unity
Press Esc to adjust the Moon section beneath the lantern panel.
Elevation: 5..85 degrees above the horizon.
Compass direction: 0..360 degrees; 0 points toward world +Z.
Light intensity: 0..1 lux; default 0.15 lux, softly cool tinted for the horror scene.
L switches global scene illumination, including moonlight. The lantern remains independent.
The star sky remains when global illumination is off. The moon disk follows its directional light.
Play Mode adjustments are temporary. For saved defaults edit the MoonLightingController on Night sky and moon in the scene Inspector.

Research and implementation:
Unity's HDRP lighting reference gives clear-sky moonlight as less than 1 lux. We use 0.15 lux as a subtle artistic starting point, not a site/date-specific lunar measurement.
https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/manual/Physical-Light-Units.html
HDRP Physically Based Sky and a directional celestial light keep the visible moon aligned with its illumination direction.
https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/manual/create-a-physically-based-sky.html
https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/manual/reference-light-component.html
Stars and the moon surface are original procedural blockout textures, not an astronomical star chart or lunar map.
The 1.2-degree moon disk is enlarged for readability; intensity remains independently adjustable in lux.
Night exposure is fixed at -2 EV for gameplay visibility. This is not a simulation of human dark adaptation.
