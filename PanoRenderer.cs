using Godot;
using System;
using System.Collections.Generic;

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
	internal int numCamPairs = 64;
	[Export]
	internal float eyeSeparation = 0.0333f;
	[Export]
	internal float near_clip=0.01f;
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
		float colAngle = (((float)numCamPairs-col) / (outTexSize/numCamPairs) * (Mathf.Pi*2/numCamPairs));
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
				//Top
				c.Rotate(c.Basis.Y,- Mathf.Pi + (float)colAngle + (float)camAngle);
				c.Rotate(c.Basis.X,-Mathf.Pi/4);
			}else{
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
				//Right eye is bottom.
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
			//That's it we're all done, we have the final image.
			//Except! No! It's perspective projection not equirectangular!?
			warpToEquirectangular();
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
	* Oh well, we must conform to the expectations of the VR
	* player demands and take our 180-degrees perspective projection
	* and turn it into an 180-degrees equirectangular projection.
	*
	* Can't honestly say I'm really sure what the difference is.
	* 
	* So How do we do that?
	*
	* Probably fastest would be some sort of shader but I don't
	* really know how to do that, so here we just take a byte[]
	* array and copy it warped into another byte array pixel
	* by damned pixel.
	* 
	* The source-pixel has the same X-coord but it's Y-coord
	* shoule be, according to people from Google good at maths:
	*
	* > float phi = fmod(y, 0.5) * 2 * 1.570796 - 0.7853982;
	* > y = tan(phi) * 0.25 + 0.25 + step(0.5, y) * 0.5;
	*
	* Two of those numbers are PI I expect, I hope Mathf has a
	* step and a fmod function or else I'll have to find out 
	* what they are....
    *
	* Or I can ask the chatbot who says:
	* double phi = Math.IEEERemainder(y, 0.5) * 2 * Mathf.PI / 2 - Mathf.PI / 4;
	* double output = Math.Tan(phi) * 0.25 + 0.25 + (y >= 0.5 ? 0.5 : 0);
	*
	* I don't know why they chose a greek letter instead of
	* a descriptive name or what phi is. Mathematicians be crazy.
	* They all like that. It's some sort of angle presumably. 
	*/
	public void warpToEquirectangular(){
		//float startTime = Time.GetTicksMsec();
		//GD.Print("Starting perspective warp at "+startTime);		
		byte[] sourceBytes = outputImg.GetData();
		byte[] destBytes = new byte[sourceBytes.Length];
		int w = outputImg.GetWidth();
		int h = outputImg.GetHeight();
		int pixellength = sourceBytes.Length/h/w;

		for(int y=0; y<h; y++){
			for(int x=0; x<w; x++){
				float dy = ((float)y)/h;
			    float phi = dy % 0.5f * 2 * Mathf.Pi / 2 - Mathf.Pi / 4;
				float sy = Mathf.Tan(phi) * 0.25f + 0.25f + (dy < 0.5f ? 0.5f : 0);
				int isy = Mathf.FloorToInt(sy * h);
				for(int col=0;col<pixellength;col++){
					      destBytes[col + pixellength * x + pixellength * w * y] = 
						sourceBytes[col + pixellength * x + pixellength * w * isy];
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


