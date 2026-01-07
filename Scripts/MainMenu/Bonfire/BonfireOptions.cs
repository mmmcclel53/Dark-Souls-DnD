using Godot;
using Godot.Collections;
using System;

public partial class BonfireOptions : VBoxContainer
{
	[Export] public Button restButton;

	[Export] public Button equipmentButton;
	[Export] public PanelContainer equipmentModal;

	[Export] public Button readyButton;

	public override void _Ready() {
		restButton.Pressed += () => { OnPressedRest(); };
		equipmentButton.Pressed += () => { OnPressedEquipment(); };
		readyButton.Pressed += () => { OnPressedReady(); };
	}

	private void OnPressedRest() {
		GD.Print("Rest");
	}

	private void OnPressedEquipment() {
		equipmentModal.Visible = true;
	}

	private void OnPressedReady() {
		GD.Print("Ready!");
	}
}
