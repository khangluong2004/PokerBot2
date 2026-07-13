using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace PokerBot2
{
    public enum WinHandType
    {
        STRAIGHT_FLUSH = 9, // Royal flush is just the highest straight flush
        QUAD = 8,
        FULL_HOUSE = 7,
        FLUSH = 6,
        STRAIGHT = 5,
        THREE_OF_A_KIND = 4,
        TWO_PAIR = 3,
        PAIR = 2,
        HIGH_CARD = 1
    }

    public class Game
    {
        // Card = 1 - 52, following the rank-suit order [2♠, 2♣, 2♦, 2♥, 3♠, ...]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetRank(int card)
        {
            return card >> 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetSuit(int card)
        {
            return (card & 0b11);
        }
        
        public static string CardToString(int card)
        {
            int rank = GetRank(card);
            int suit = GetSuit(card);
            string display = ""; 
            if (rank <= 8)
            {
                display = "" + ('1' + rank);
            } else
            {
                switch (rank)
                {
                    case 9: display = "J"; break;
                    case 10: display = "Q"; break;
                    case 11: display = "K"; break;
                    case 12: display = "A"; break;
                }
            }
            
            switch (suit)
            {
                case 0: display += '♠'; break;
                case 1: display += '♣'; break;
                case 2: display += '♦'; break;
                case 3: display += '♥'; break;
            }
            return display;
        }

        // Card needs to be 2 char, rank followed by suit, eg: '2♠' or 'A♠'
        public static int StringToCard(string card)
        {
            int rank; 
            switch (card[0])
            {
                case 'J': rank = 9; break;
                case 'Q': rank = 10; break;
                case 'K': rank = 11; break;
                case 'A': rank = 12; break;
                default: rank = card[0] - '2'; break;
            }
            
            var suit = card[1] switch
            {
                '♠' => 0,
                '♣' => 1,
                '♦' => 2,
                '♥' => 3,
                _ => throw new Exception("Invalid suit"),
            };
            return (rank * 4 + suit);
        }

        private int[] Deck = [.. Enumerable.Range(0, 52)];
        private int CurDealtCard = 0;
        private readonly int NumPlayer;

        public Game(int numPlayer)
        {
            Random.Shared.Shuffle(Deck);
            NumPlayer = numPlayer;
        }

        public void Reset()
        {
            Random.Shared.Shuffle(Deck);
            CurDealtCard = 0;
        }

        // The first 2 * Numplayers are the dealt card for player
        // Should call this 2N times for hole card,
        // then 3 times for flop, 1 for turn, 1 for river
        public int DealCard()
        {
            return Deck[CurDealtCard++];
        }

        // input = any hands (max 7 cards, min 1 card)
        // Return (highest rank, win hand type)
        // Note: input will be sort in place
        public static (int, WinHandType) EvalHand(Span<int> cards)
        {
            // Sorted by rank, due to the ordering defined
            cards.Sort();
            int prevSuit = GetSuit(cards[0]);
            int prevRank = GetRank(cards[0]);

            Span<int> highestSameRank = stackalloc int[4];
            highestSameRank[0] = highestSameRank[1] = highestSameRank[2] = highestSameRank[3] = -1;
            int sameRankCount = 0;
            int highestPairNotHighestTrip = -1;

            int highestInStraight = -1;
            int straightCount = 1;
            int highestInStraightFlush = -1;

            Span<int> flushCount = stackalloc int[4];
            flushCount[prevSuit] = 1;
            Span<int> highestInFlush = stackalloc int[4];
            // Don't need highestInFlush, since it only matters after 5 cards

            //TODO: Handle straight starting with ace
            int diffSuitIndex = 0;
            int curSuit, curRank, rankDiff;
            for (int i=1; i < cards.Length; i++)
            {
                curSuit = GetSuit(cards[i]);
                curRank = GetRank(cards[i]);

                // Flush
                flushCount[curSuit] += 1;
                highestInFlush[curSuit] = curRank;

                // Straight
                rankDiff = curRank - prevRank;
                if (rankDiff == 1)
                {
                    straightCount++;
                } else if (rankDiff != 0)
                {
                    straightCount = 1;
                }

                // Straight flush + Straight
                if (curSuit != prevSuit)
                {
                    diffSuitIndex = i;
                }

                if (straightCount >= 5)
                {
                    highestInStraight = curRank;
                    if (i - diffSuitIndex + 1 >= 5)
                    {
                        highestInStraightFlush = curRank;
                    }
                }

                // Pair + Trip + Quad
                if (rankDiff == 0)
                {
                    sameRankCount += 1;
                }
                else
                {
                    sameRankCount = 0;
                }

                // Copy the old highest card of [rank] to [rank-1] to still retain
                // the highest pair that's not a trip to handle full house
                // highestPairNotTrip will remain -1 if there is no pair that's not trip
                if (sameRankCount == 1)
                {
                    highestPairNotHighestTrip = highestSameRank[1];
                }
                highestSameRank[sameRankCount] = curRank;

                prevRank = curRank;
                prevSuit = curSuit;
            }

            // Return from top hand to bottom
            if (highestInStraightFlush > 0)
            {
                return (highestInStraightFlush, WinHandType.STRAIGHT_FLUSH);
            }

            if (highestSameRank[3] >= 0)
            {
                return (highestSameRank[3], WinHandType.QUAD);
            }

            // For full house, encode the pair highest in the last 6 bits, and the trip highest in the next 6 bits
            // The highest pair that's not a trip is stored in highestSameRank[1]
            if (highestPairNotHighestTrip == highestSameRank[2])
            {
                highestPairNotHighestTrip = highestSameRank[1];
            }

            if (highestSameRank[2] >= 0 && highestPairNotHighestTrip >= 0)
            {
                return ((highestSameRank[2] << 6) + highestPairNotHighestTrip, WinHandType.FULL_HOUSE);
            }

            // Flush. Manual loop unroll :0 Fuck the jit
            int maxFlush = -1;
            if (flushCount[0] >= 5 && highestInFlush[0] > maxFlush)
            {
                maxFlush = highestInFlush[0];
            }

            if (flushCount[1] >= 5 && highestInFlush[1] > maxFlush)
            {
                maxFlush = highestInFlush[1];
            }

            if (flushCount[2] >= 5 && highestInFlush[2] > maxFlush)
            {
                maxFlush = highestInFlush[2];
            }

            if (flushCount[3] >= 5 && highestInFlush[3] > maxFlush)
            {
                maxFlush = highestInFlush[3];
            }

            if (maxFlush >= 0)
            {
                return (maxFlush, WinHandType.FLUSH);
            }

            // Straight
            if (highestInStraight >= 0)
            {
                return (highestInStraight, WinHandType.STRAIGHT);
            }

            if (highestSameRank[2] >= 0)
            {
                return (highestSameRank[2], WinHandType.THREE_OF_A_KIND);
            }

            // TODO: Handle 2 pairs

            if (highestSameRank[1] >= 0)
            {
                return (highestSameRank[1], WinHandType.PAIR);
            }

            return (cards[^1], WinHandType.HIGH_CARD);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // playerNum starts from 0
        private (int, WinHandType) EvalPlayer(int playerIndex) {
            Span<int> cards = stackalloc int[7];
            cards[0] = Deck[playerIndex * 2];
            cards[1] = Deck[playerIndex * 2 + 1];
            int publicStart = NumPlayer * 2;
            cards[2] = Deck[publicStart];
            cards[3] = Deck[publicStart + 1];
            cards[4] = Deck[publicStart + 2];
            cards[5] = Deck[publicStart + 3];
            cards[6] = Deck[publicStart + 4];

            return EvalHand(cards);
        }

        // TODO: Compile-time number of players for public int GetWinner()

        
    }
}
