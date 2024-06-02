using Godot;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

/**
* A panorama renderer which will create an image such as you
* can feed into a VR headset to put a person into your scene.
* 
* Beacuse the aim is not to swivel eyes 360 degrees in their
* sockets, but to orbit them around a central point, we can't
* just use a cube-map. The eyes origin has to be different for
* each vertical column of pixels in the output image.
*
* The approach here is to have a pair of cameras for every
* single column of pixels in the output image, but that is too
* memory intensive and 16,000 cameras in a scene is crazed.
*
* We we accept it will take many frames to scan the camera
* around, such as you might with a phone taking a panorama picture.
*
* You'll have to pause your game so time stops, 
* call to start the renderer, and check each frame if it's finished
* when it has you can save the image and unpause the game.
*
* If you unpause for just one frame and then pause againt to do 
* another panorama you'll have a 360 3d rendering for VR video
* of what it's like to be in your game.
*
* This script should be attached to a Node3D,
* it will create an array of cameras numCameras big and
* when you start the render it will rotate them around
* capturing the result to a texture. After a final warp
* to correct to equirectangular projection the image
* is available as outputImg to save.
*
* The cameras are kept in sync with the global position
* of the node the script is attached to, so moving it
* moves the viewpoint of the panorama caputre.
* 
*/
public partial class PanoRenderer : Node3D {
	
	public List<Camera3D> cams = new List<Camera3D>();
	public List<SubViewport> viewports = new List<SubViewport>();
	public Image outputImg;
	internal int colNum = -1;
	[Export]
	internal int outTexSize = 4096;
	[Export]
	internal int numCamPairs = 8;
	[Export]
	internal float eyeSeparation = 0.03f;
	[Export]
	internal float near_clip=0.03f;
	[Export]
	internal float far_clip=10000f;
	[Export]
	internal uint maskLayers = 1+2+4+8+16+32+64+128+256+512+1024+2048+4096+8192+16384+32768+65536+131072; //262144+524288
  	internal bool camsAttached=false;


	/**
	* Start
	*/
	public override void _Ready() {
		outputImg = new Image();
		byte[] initImg = new byte[outTexSize*outTexSize * 3];
		outputImg.SetData(outTexSize,outTexSize,false,Image.Format.Rgb8,initImg);
		for(int n=0;n<numCamPairs*2*2;n++){				//Two eyes, two cameras per eye (one up, one down)
			SubViewport s = new SubViewport();
			s.Size = new Vector2I(5,outTexSize/4);		//If we have a 1-pixel image here, we lose all the lighting from the scene!? Lowest we can go is 5.
			s.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
			s.PositionalShadowAtlasSize = 4096;
			s.CanvasCullMask=maskLayers;
			this.AddChild(s);
			Camera3D c = new Camera3D();
			c.Fov = 90;
			c.Near = near_clip;
			c.Far = far_clip;
			c.KeepAspect = Camera3D.KeepAspectEnum.Height;
			c.CullMask = maskLayers;
			s.AddChild(c);
			cams.Add(c);
			viewports.Add(s);
		}
		camsAttached=true;
		removeCams();
	}

	/**
	* Update
	*/
	public override void _Process(double delta) {
		if(colNum>=0){
			addCams();
			setupCamPos(colNum);
	    RenderingServer.FramePostDraw+=doCaptureAfterRender;
		}else{
			removeCams();
		}
	}


	/**
	* Add the cams and view ports to the node tree
	* we are doing a render
	*/
	public void addCams(){
		if(!camsAttached){
			camsAttached=true;
			foreach(SubViewport s in viewports){
				s.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
				this.AddChild(s);
			}
		}
	}


	/**
	* Remove the cams and view ports from the node tree
	* we are doing a render
	*/
	public void removeCams(){
		if(camsAttached){
			camsAttached=false;
			foreach(SubViewport s in viewports){
				this.RemoveChild(s);
				s.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
			}
		}
	}
	

	/**
	* Render a single column.
	* We set the cameras into position and 
	* set up a function to be called post-render
	* to save the column to the output texture
	*
	* Why is a +ve y-rotation an anticlockwise rotation
	* in Godot-space? Because every 3d engine picks one
	* axis to get wrong and Godot points it's Z backwards.
	*
	* Thuse we have to start with a high rotation and bring
	* it down to a low one.
	*/
	private void setupCamPos(int col){
		int i=0;
		float colAngle = (((float)numCamPairs-col) / (outTexSize/numCamPairs) * (Mathf.Pi*2/numCamPairs)) + MathF.PI;
		foreach(Camera3D c in cams){
			int dir = i%numCamPairs;
			float camAngle = (numCamPairs-dir) * ((MathF.PI*2)/numCamPairs);
			c.GlobalPosition = GlobalPosition;
			c.GlobalRotation = GlobalRotation;
			if(i<(cams.Count/2)){
				//Left Eye
				c.Position += new Vector3(-Mathf.Cos(camAngle+colAngle), 0, Mathf.Sin(camAngle+colAngle)) * eyeSeparation;
			}else{
				//Right Eye
				c.Position -= new Vector3(-Mathf.Cos(camAngle+colAngle), 0, Mathf.Sin(camAngle+colAngle)) * eyeSeparation;
			}
			if((i%(numCamPairs*2))<numCamPairs){
				//Top of timage
				c.Rotate(c.Basis.Y,- Mathf.Pi + (float)colAngle + (float)camAngle);
				c.Rotate(c.Basis.X,-Mathf.Pi/4);
			}else{
				//Bottom of image
				c.Rotate(c.Basis.Y,- Mathf.Pi + (float)colAngle + (float)camAngle);
				c.Rotate(c.Basis.X,+Mathf.Pi/4);
			}
			i++;
		}
	}

	/**
	* After the frame is rendered, we need to copy the contents
	* of their viewports to the output texture
	*/
	private void doCaptureAfterRender(){
        RenderingServer.FramePostDraw-=doCaptureAfterRender;
		int i=0;
		foreach(SubViewport vp in viewports){
			Texture2D t = vp.GetTexture();
			Image sourceImage = t.GetImage();
			int sx = Mathf.FloorToInt(sourceImage.GetWidth()/2);

			int x=colNum;
			int dir = i%numCamPairs;
			x+=dir*(outTexSize/numCamPairs);
			int y=0;
			if(i>=(viewports.Count/2)){
				//One eye is top, one bottom.
				y=outTexSize/2;
			}
			if((i%(numCamPairs*2))<numCamPairs){
				//Bottom half of two-cam view
				y+=outTexSize/4;
			}
			outputImg.BlitRect(sourceImage,new Rect2I(sx,0,1,1024),new Vector2I(x,y));
			i++;
		}

		if(colNum<(outTexSize/numCamPairs)){
			colNum++;
		}else{
			//It's finished! One final step..
			warpToEquirectangular();
			//Then mark as done.
			colNum=-1;
		}
	}


	/**
	* Warping from a perspective projection we get from the camera into a
	* equirectanular one that the VR video/image players all want.
	* 
	* Why doesn't the player just show a perspective-projection image
	* eh? Why we gotta warp it to make it equirectangular when that
	* is definitely going to lose detail and precision now innit?
	*
	* It's not a big change, but I think it maybe helps a lot
	* with looking at strange angles, especially for binocular
	* vision when looking up or down. Proportions look slightly off.
	*
	* So we have to morph from the old image into the new.
	* Row by row. We can go one row at a time, find which 
	* source-row to copy into the row in the dest image and
	* then copy it pixel by pixel. Worse. Colour-component by colour-compnent.
	*
	* Lets try: [ https://www.videopoetics.com/tutorials/capturing-stereoscopic-panoramas-unity/ ]
	* > float phi = fmod(y, 0.5) * 2 * 1.570796 - 0.7853982;
	* > y = tan(phi) * 0.25 + 0.25 + step(0.5, y) * 0.5;
	*
	* That turned out to be doing the exact opposite of what we 
	* want I think? It stretched it vertically so the round
	* things looked like elipses and the rectangles morphed as
	* they moved.
	*
	* Reversing it here is, better? Still not sure it's right
	* really. Not even entirely sure it's better than just
	* the perspective projection without any post-frame morph at all.
	*
	* But this is the best I got. Maths people please do submit
	* better code, I ain't got a clue what this is doing really.
	*/
	public void warpToEquirectangular(){
		//float startTime = Time.GetTicksMsec();
		//GD.Print("Starting perspective warp at "+startTime);		
		byte[] sourceBytes = outputImg.GetData();
		byte[] destBytes = new byte[sourceBytes.Length];
		int w = outputImg.GetWidth();
		int h = outputImg.GetHeight();
		int h2=h>>1;
		int pixellength = sourceBytes.Length/h/w;

		for(int eye=0;eye<2;eye++){
			for(int y=0; y<h2; y++){
				float dy = ((float)y)/(h2);

				float t = (dy - 0.5f) / 0.5f;
				float tmp = Mathf.Atan(t);
				float sourceY = (tmp + (Mathf.Pi / 4)) / (Mathf.Pi/2);

				int sourceYPix = Mathf.FloorToInt(sourceY * h2);
				if(sourceYPix>h2-1){
					sourceYPix=h2-1;
				}
				if(sourceYPix<0){
					sourceYPix=0;
				}
				for(int x=0; x<w; x++){
					for(int col=0;col<pixellength;col++){
						  destBytes[col + (pixellength * x) + (pixellength * w *          y) + (eye * h2 * w * pixellength)] = 
						sourceBytes[col + (pixellength * x) + (pixellength * w * sourceYPix) + (eye * h2 * w * pixellength)];
					}
				}
			}
		}
		outputImg.SetData(w,h,false,outputImg.GetFormat(),destBytes);
		//float endTime = Time.GetTicksMsec();
		//float diffTime = endTime-startTime;
		//GD.Print("Ending perspective warp at "+endTime);		
		//GD.Print("I mean you could try and rewrite as a shader, but you'd only save "+diffTime+" ms per frame");   //756 on my machine 		
	}

	/**
	* Here's the original code doing the warp as described
	* in the Unity/Google paper. As I say, this is definitely
	* worse than just the perspective projection with no warp
	* at all, but seems to be doing bascially the oposite of
	* what we want? 
	*/	
	public void warpToEquirectangularOldBackwards(){
		byte[] sourceBytes = outputImg.GetData();
		byte[] destBytes = new byte[sourceBytes.Length];
		int w = outputImg.GetWidth();
		int h = outputImg.GetHeight();
		int h2=h>>1;
		int pixellength = sourceBytes.Length/h/w;

		for(int eye=0;eye<2;eye++){
			for(int y=0; y<h2; y++){
				float dy = ((float)y)/(h2);

				float tmp = y * (Mathf.Pi/2) - (Mathf.Pi / 4);
				float sourceY = Mathf.Tan(tmp) * 0.5f + 0.5f;

				int sourceYPix = Mathf.FloorToInt(sourceY * h2);
				if(sourceYPix>h2-1){
					sourceYPix=h2-1;
				}
				if(sourceYPix<0){
					sourceYPix=0;
				}
				for(int x=0; x<w; x++){
					for(int col=0;col<pixellength;col++){
						  destBytes[col + (pixellength * x) + (pixellength * w *          y) + (eye * h2 * w * pixellength)] = 
						sourceBytes[col + (pixellength * x) + (pixellength * w * sourceYPix) + (eye * h2 * w * pixellength)];
					}
				}
			}
		}
		outputImg.SetData(w,h,false,outputImg.GetFormat(),destBytes);
	}


	/**
	* Start a render, it'll take many frames
	* so hope the caller has paused everything somehow.
	*/
	public void startRender(){
		colNum=0;
	}

	/**
	* The caller will want to know when their photo is
	* ready so they'll keep pestering us every frame 
	* until we relent
	*/
	public bool isFinished(){
		return (colNum<0);
	}

}

