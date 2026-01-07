using Godot;
using Godot.Collections;
using System;

public partial class EquipmentSelect : PanelContainer
{
	[Export] public PanelContainer equipmentModal;

	[Export] public Button closeButton;

	[Export] public TextEdit searchField;
	[Export] public OptionButton sortSelect;

	[Export] public GridContainer equipmentContainer;

	public override void _Ready() {
		closeButton.Pressed += () => { OnClose(); };
		searchField.TextChanged += () => { OnSearchChange(); };
		sortSelect.ItemSelected += (index) => { OnSortChange(index); };
	}

	private void OnClose() {
		equipmentModal.Visible = false;
	}

	private void OnSearchChange() {
		GD.Print(searchField.Text);
	}

	private void OnSortChange(long index) {

	}
}
