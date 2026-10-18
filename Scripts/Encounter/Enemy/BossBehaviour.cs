using Godot;
using System.Collections.Generic;
using System.Linq;

// A boss's behaviour deck and facing for one fight (p27-29).
//
// The deck is `deckSize` random starting cards, shuffled. Each activation turns over the top
// card onto a face-up discard pile. When the deck runs out the discard pile is turned face
// down without shuffling, so the boss repeats the pattern it just played, in the same order:
// learning that pattern is the point.
//
// Heat up (p28): once the boss's health falls to its heat up point, one random Heat Up card
// goes into the deck. Matt's call (Oct 2026): it is shuffled into the whole deck, the discard
// pile included, so the party really does relearn the pattern. The card last played stays the
// reference for the weak arcs (`lastCard`) until the next draw, although it is back in the deck.
//
// Cards are named by their index in BossData.cards, which is how a snapshot saves them.
public class BossBehaviour
{
	public readonly BossData data;
	public Vector2I facing;
	// Top first.
	public readonly List<BossCard> deck = new List<BossCard>();
	// Played this time through the deck, oldest first; the last is the top of the pile.
	public readonly List<BossCard> discard = new List<BossCard>();
	public BossCard lastCard { get; private set; }
	public bool heated { get; private set; }

	private static readonly RandomNumberGenerator rng = new RandomNumberGenerator();

	private BossBehaviour(BossData data, Vector2I facing) {
		this.data = data;
		this.facing = facing;
	}

	public static BossBehaviour Start(BossData data, Vector2I facing) {
		rng.Randomize();
		BossBehaviour behaviour = new BossBehaviour(data, facing);
		List<BossCard> starting = data.cards.Where(c => c != null && !c.heatUp).ToList();
		Shuffle(starting);
		behaviour.deck.AddRange(starting.Take(Mathf.Max(1, data.deckSize)));
		return behaviour;
	}

	// The weak arcs of the last attack on the card on top of the discard pile (p28).
	public BossAction.Arcs WeakArcs => lastCard?.LastAttack?.WeakArcs ?? BossAction.Arcs.NONE;
	public BossAction.Arcs AttackedArcs => lastCard?.LastAttack?.AttackedArcs ?? BossAction.Arcs.NONE;

	public int CardsLeft => deck.Count;

	public BossCard Draw() {
		if (deck.Count == 0) {
			deck.AddRange(discard);
			discard.Clear();
		}
		if (deck.Count == 0) return null;
		BossCard card = deck[0];
		deck.RemoveAt(0);
		discard.Add(card);
		lastCard = card;
		return card;
	}

	public bool ShouldHeatUp(int health) => !heated && data.heatUp > 0 && health > 0 && health <= data.heatUp;

	public void HeatUp() {
		if (heated) return;
		heated = true;
		List<BossCard> heatUps = data.cards.Where(c => c != null && c.heatUp).ToList();
		if (heatUps.Count > 0) {
			rng.Randomize();
			deck.Add(heatUps[rng.RandiRange(0, heatUps.Count - 1)]);
		}
		deck.AddRange(discard);
		discard.Clear();
		Shuffle(deck);
	}

	// Old Dragonslayer's Corrupted Dragonslayer: one random Heat Up card not yet in play, on top
	// of the deck so the next activation plays it. False when all of them are in already.
	public bool HeatUpOnTop() {
		List<BossCard> unused = data.cards.Where(c => c != null && c.heatUp && !deck.Contains(c) && !discard.Contains(c)).ToList();
		if (unused.Count == 0) return false;
		rng.Randomize();
		deck.Insert(0, unused[rng.RandiRange(0, unused.Count - 1)]);
		heated = true;
		return true;
	}

	private static void Shuffle(List<BossCard> cards) {
		for (int i = cards.Count - 1; i > 0; i--) {
			int j = rng.RandiRange(0, i);
			(cards[i], cards[j]) = (cards[j], cards[i]);
		}
	}

	// ----- Saving -----

	public int[] DeckIndices => deck.Select(c => data.cards.IndexOf(c)).ToArray();
	public int[] DiscardIndices => discard.Select(c => data.cards.IndexOf(c)).ToArray();
	public int LastIndex => lastCard == null ? -1 : data.cards.IndexOf(lastCard);

	public static BossBehaviour Restore(BossData data, Vector2I facing, int[] deck, int[] discard, int last, bool heated) {
		BossBehaviour behaviour = new BossBehaviour(data, facing) { heated = heated };
		foreach (int i in deck ?? new int[0]) if (i >= 0 && i < data.cards.Count) behaviour.deck.Add(data.cards[i]);
		foreach (int i in discard ?? new int[0]) if (i >= 0 && i < data.cards.Count) behaviour.discard.Add(data.cards[i]);
		behaviour.lastCard = last >= 0 && last < data.cards.Count ? data.cards[last] : null;
		return behaviour;
	}
}
