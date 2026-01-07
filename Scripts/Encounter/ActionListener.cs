using Godot;
using Godot.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public partial class ActionListener : Node
{
	private const float MAGIC_SIZE = 360 * 2;

	[Export] public Array<PackedScene> players;
	[Export] public Array<PackedScene> enemies;
	[Export] public Node nodesParent;

	private int playersSpawned = 0;
	private List<GameNode> entrances = new List<GameNode>();

	private int activeEnemyNum = 0;
	
	public void ToggleAllNodesOff() {
		foreach (Node node in nodesParent.GetChildren()) {
			Control button = (Control)node.GetChild(0);
			button.Modulate = new Color(0,0,0,0);
		}
	}

	public override void _Ready() {
		ToggleAllNodesOff();
	    SpawnEnemies();
	}

	public void SpawnEnemies() {
	    Random random = new Random();
	    List<GameNode> enemyNodes = new List<GameNode>();

	    foreach (GameNode gameNode in nodesParent.GetChildren()) {
	        EncounterManager.nodes.Add(gameNode);
	        if (gameNode.isEnemySpawn) {
	            enemyNodes.Add(gameNode);
	        }

	        // Needs choice here
	        if (gameNode.isEntrance) {
	            entrances.Add(gameNode);
	        }
	    }

	    // Spawn randomly
	    foreach (PackedScene e in enemies) {
	        int i = random.Next(enemyNodes.Count);
	        Control node = enemyNodes[i];
			Node2D enemy = (Node2D)e.Instantiate();
			GD.Print(node.Name);

			float nodeSize = node.Size.X;
			enemy.Scale = new Vector2(nodeSize / MAGIC_SIZE, nodeSize / MAGIC_SIZE);
			EncounterManager.MovePlayer(enemy, node, null);
	        EncounterManager.enemies.Add(enemy);
	    }

	    EncounterManager.enemies = EncounterManager.enemies.OrderByDescending(e => (e.GetChild(0) as Enemy).threatLevel).ToList();
	    EncounterManager.action = EncounterManager.Action.PICK_ENTRANCE;
	}

	public void PickEntrance() {
	    if (playersSpawned >= players.Count) {
	        return;
	    }

	    foreach (GameNode entrance in entrances) {
			entrance.ToggleButton(true);
			BaseButton button = (BaseButton)entrance.GetChild(0);
			button.Pressed += () => { SpawnPlayer(entrance); };
	    }
	}
	public void SpawnPlayer(Control entrance) {
	    PackedScene p = players[playersSpawned];
		Node2D player = (Node2D)p.Instantiate();
		GD.Print(entrance.Name);

		float nodeSize = entrance.Size.X;
		player.Scale = new Vector2(nodeSize / MAGIC_SIZE, nodeSize / MAGIC_SIZE);
		EncounterManager.MovePlayer(player, entrance, null);
	    (player.GetChild(0) as Player).isAggro = playersSpawned == 0 ? true : false;
	    EncounterManager.players.Add(player);
	    // player.GetComponent<Player>().setLocation(entrance.transform.position);
	    playersSpawned++;

	    if (playersSpawned == players.Count) {
	        foreach (Node e in entrances) {
	            (e as GameNode).ToggleButton(false);
	        }
	        EncounterManager.action = EncounterManager.Action.ENEMY_MOVE; 
	    }
	}

	public void EnemyMove() {
	    Node2D aggroPlayer = EncounterManager.players[0];
	    List<Node2D> nonAggroPlayers = new List<Node2D>();
	    foreach (Node2D playerObj in EncounterManager.players) {
	        Player player = (Player) playerObj.GetChild(0);
	        if (player.isAggro) {
	            aggroPlayer = playerObj;
	        } else {
	            nonAggroPlayers.Add(playerObj);
	        }
	    }

	    Enemy enemy = (Enemy) EncounterManager.enemies[activeEnemyNum].GetChild(0);
	    enemy.DoMoves(aggroPlayer, nonAggroPlayers);
	}

	public override void _Process(double delta) {
	    if (EncounterManager.action == EncounterManager.Action.PICK_ENTRANCE) {
	        PickEntrance();
	        EncounterManager.action = EncounterManager.Action.INACTIVE;
	    }

	    if (EncounterManager.action == EncounterManager.Action.ENEMY_MOVE) {
	        EnemyMove();
	        EncounterManager.action = EncounterManager.Action.INACTIVE;

	        // Grab next active enemy
	        activeEnemyNum = (activeEnemyNum + 1) % EncounterManager.enemies.Count;
	    }
	}
}
