# Godot Panorama Renderer.

## Rendering a 3D Stereoscopic 360 degree view of the scene.

Rendering an equirectangular 360-degree 3d-stereoscopic 
view of a scene is tricky because the offset from the 
origin of the cameras needs to change for every vertical
pixel on the screen.

As explained very well in [this amazing page by shubhmehta](https://shubhmehta.artstation.com/pages/vr-360-rendering), if you are not careful what you'll get is a view from two eyes swiveling in their sockets:

![](https://cdnb.artstation.com/p/content_assets/assets/000/069/223/original/e28a3ab20e2b67a0a316aae3a10719a8.gif?1522536171) 
 
when what we need is a map from a head rotating two eye-cams:
 
![](https://cdna.artstation.com/p/content_assets/assets/000/069/224/original/51f4c6ad58f5bf255af6e541a4bc97ba.gif?1522536191)

## Our approach

We create two pairs of cameras, point one 45 degrees to the sky and one 45 degrees to the floor, then rotate them around like doing a panorama shot with your phone.

Which means you'll need to pause your game while you take a panoramic photograph or you'll get similar duplicated people and elongated dogs etc.

It's going to take quite a few frames.

We reduce the number by having more than one camera. You can set what number you want. I found after about 60 adding more cameras became less useful as it slowed down the each-frame render so much that doing fewer-frames wasn't helpful.

## Project to Equirectangular

That gives us a perspective projection, and the VR headsets all want an equirectangular projection, so we do a final pass over the image when it's complete to warp it to make it equirectangular.

## Who would want this

You may want to create a background panorama of your scene for environment mapping, or just want to make a whole-world screenshot someone can see in VR, or you might want to stitch thousands of them together to make a VR movie. That's why I needed it.

## Licence

Same licence as Godot, whatever that is.

## Install

All you really need is PanoRenderer.cs which you can attach to a Node3D in your scene.

When your code wants to start a render, move that Node3d to the view-point, and call the StartRender function of the PanoRenderer.

Then call isFinished() each frame to see if it's finished.

The other files here are a demo scene if you want to check it out in it's own project first.
