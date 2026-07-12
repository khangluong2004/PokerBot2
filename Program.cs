using PokerBot2;

// '♠♣♦♥'
int[] hand = [
    Game.StringToCard("2♠"),
    Game.StringToCard("2♣"),
    Game.StringToCard("3♣"),
    Game.StringToCard("2♦"),
    Game.StringToCard("A♠"),
    Game.StringToCard("2♥"),
];

Console.WriteLine(string.Join(", ", hand));
Console.WriteLine(Game.EvalHand(hand));
