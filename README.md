# ISM_Unity
 An image-source reverberation model for Unity.
 
This work is done for CS-E5530 - Virtual Acoustics, Aalto University, 07.01.2020-04.03.2020.

See various branches for features. The basic image-source model is found on `master`, while bells and whistles were added on `flipping-pressure` (flipping pressure contribution of reflections in the IR, absorption materials for different surfaces). Two parallel versions were implemented on `parellel-raycast` and `parellel-raycast-2` (experimental, actual performance gains require more work...). 

The `ray-tracer` branch is an exercise in the diffuse reflection contribution to the ISM. Each commit pregressively adds features: basic iterative ray tracing to determine diffuse contribution, occlusion, source directivity, ray tracing visualization, and absorption/diffuse-specular propagation ratios.
