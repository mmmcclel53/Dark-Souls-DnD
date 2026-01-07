using Godot;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;

public static partial class EncounterManager {

    public enum StatusEffect { BLEED, POISON, FROST, STAGGER, NONE };

    public enum Action { INACTIVE, PICK_ENTRANCE, ENEMY_MOVE };
    public static Action action = Action.INACTIVE;

    public static int gridSize = 7;
    public static List<Control> nodes = new List<Control>();

    public static List<Node2D> players = new List<Node2D>();
    public static bool isPlayerMoving = false;

    public static List<Node2D> enemies = new List<Node2D>();
    public static bool isEnemyMoving = false;

    // Settings
    public static bool showEnemyInfo = true;

    public static List<Node2D> GetAllPlayersInNode(Control node) {
        List<Node2D> result = new List<Node2D>();
        foreach (Node child in node.GetChildren()) {
            if (child.IsInGroup("Player") || child.IsInGroup("Enemy")) {
                result.Add(child as Node2D);
            }
        }
        return result;
    }

    public static List<Node2D> GetPlayersInNode(Control node, string groupName) {
        List<Node2D> result = new List<Node2D>();
        foreach (Node child in node.GetChildren()) {
            if (child.IsInGroup(groupName)) {
                result.Add(child as Node2D);
            }
        }
        return result;
    }

    // Unelegent piece of shit code
    public static void FixPositioning(Control node) {
        float nodeSize = node.Size.X;
        List<Node2D> allPlayers = GetAllPlayersInNode(node);
        if (allPlayers.Count == 1) {
            allPlayers[0].Position = new Vector2(nodeSize/4,nodeSize/4);
        } else if (allPlayers.Count == 2) {
            allPlayers[0].Position = new Vector2(0,nodeSize/4);
            allPlayers[1].Position = new Vector2(nodeSize/2,nodeSize/4);
        } else if (allPlayers.Count == 3) {
            allPlayers[0].Position = new Vector2(0,0);
            allPlayers[1].Position = new Vector2(nodeSize/2,0);
            allPlayers[2].Position = new Vector2(nodeSize/4,nodeSize/2);
        }
    }

    public static void MovePlayer(Node2D obj, Control newNode, Control oldNode) {
        if (oldNode != null) {
            oldNode.RemoveChild(obj);
            FixPositioning(oldNode);
        }

        Node btn = newNode.GetChild(-1);
        newNode.AddChild(obj);
        newNode.MoveChild(btn, -1);
        FixPositioning(newNode);        
    }
}
