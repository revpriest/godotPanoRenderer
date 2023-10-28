using Godot;
using System;

public partial class PanoDemoCode : Node3D {
	[Export]
	private PanoRenderer panoRenderer = null;
	private bool started=false;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready(){
		if(panoRenderer==null){
			panoRenderer = GetNode<PanoRenderer>("PanoRenderer");
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta) {
		if(started){
			if(panoRenderer.isFinished()){
				string fname="OutputPanorama.png";
				GD.Print("Saving panaroma image:"+fname);
				panoRenderer.outputImg.SavePng(fname);
				started=false;
			}
		}

	}

	/**
    * Keyboard and input Event Handler
    */
    public override void _Input(InputEvent @event){
        if(@event is InputEventKey){
            InputEventKey keyEvent = (InputEventKey)@event;
            string s = OS.GetKeycodeString(keyEvent.Keycode);
			if((s=="R")||(s=="r")){
				if(started){
					GD.Print("Not finished rendering yet");
				}else{
					GD.Print("Starting Panorama Render");
					panoRenderer.startRender();
					started=true;
				}
			}
        }
    }

}
