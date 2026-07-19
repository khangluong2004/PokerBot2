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

        // input = 7 cards (if not enough, add negative filler cards, starting from -4 downward,
        // avoid confusion with 2)
        // Return (ordering value, win hand type), where the ordering value
        // is used to sort the win hand of the same type (higher = better, equal = same hand)
        // Note: input will be sort in place
        public static (int, WinHandType) EvalHand(Span<int> cards)
        {
            // Sorted by rank, due to the ordering defined
            cards.Sort();
            int prevSuit = GetSuit(cards[0]);
            int prevRank = GetRank(cards[0]);
            int orderingVal;

            Span<int> highestSameRank = stackalloc int[4];
            highestSameRank[0] = highestSameRank[1] = highestSameRank[2] = highestSameRank[3] = -1;
            int sameRankCount = 0;
            int highestPairNotHighestTrip = -1; // Store the second highest pair

            int highestInStraight = -1;
            int straightCount = 1;

            // Straight flush - Per suit computation
            Span<int> highestSoFarInStraightFlush = stackalloc int[4];
            highestSoFarInStraightFlush[0]
                = highestSoFarInStraightFlush[1]
                = highestSoFarInStraightFlush[2]
                = highestSoFarInStraightFlush[3]
                = -1;
            highestSoFarInStraightFlush[prevSuit] = prevRank;
            Span<int> highestInStraightFlush = stackalloc int[4];
            highestInStraightFlush[0]
                = highestInStraightFlush[1]
                = highestInStraightFlush[2]
                = highestInStraightFlush[3]
                = -1;
            Span<int> countStraightFlush = stackalloc int[4];
            countStraightFlush[prevSuit] = 1;
            int rankDiffStraightFlush;

            Span<int> flushCount = stackalloc int[4];
            flushCount[prevSuit] = 1;
            Span<int> orderingValueFlush = stackalloc int[4]; // Store the ordering value of flush
            orderingValueFlush[prevSuit] = prevRank;

            
            int curSuit, curRank, rankDiff;
            for (int i=1; i < cards.Length; i++)
            {
                // Skip filler cards
                if (cards[i] < 0)
                {
                    continue;
                }

                curSuit = GetSuit(cards[i]);
                curRank = GetRank(cards[i]);

                // Straight flush
                // Like straight, but for each suit
                rankDiffStraightFlush = highestSoFarInStraightFlush[curSuit] - curRank;
                if (rankDiffStraightFlush == 1)
                {
                    countStraightFlush[curSuit]++;
                    highestSoFarInStraightFlush[curSuit] = curRank;
                    if (countStraightFlush[curSuit] >= 5)
                    {
                        highestInStraightFlush[curSuit] = curRank;
                    }
                } else if (rankDiffStraightFlush != 0) {
                    countStraightFlush[curSuit] = 1;
                    // Handle straight start with ace then 2
                    if (highestSoFarInStraightFlush[curSuit] == 12 && curRank == 0)
                    {
                        countStraightFlush[curSuit] = 2;
                    }
                }


                // Flush
                flushCount[curSuit] += 1;
                orderingValueFlush[curSuit] += curRank << (6 * (flushCount[curSuit] - 1));
                

                // Straight
                rankDiff = curRank - prevRank;
                if (rankDiff == 1)
                {
                    straightCount++;
                    if (straightCount >= 5)
                    {
                        highestInStraight = curRank;
                    }
                } else if (rankDiff != 0)
                {
                    straightCount = 1;
                    // Handle straight starting from ace
                    if (prevRank == 12 && curRank == 0)
                    {
                        straightCount = 2;
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
            // Straight flush manual unroll
            int maxStraightFlush = highestInStraightFlush[0];
            if (highestInStraightFlush[1] > maxStraightFlush)
            {
                maxStraightFlush = highestInStraightFlush[1];
            }

            if (highestInStraightFlush[2] > maxStraightFlush)
            {
                maxStraightFlush = highestInStraightFlush[2];
            }

            if (highestInStraightFlush[3] > maxStraightFlush)
            {
                maxStraightFlush = highestInStraightFlush[3];
            }

            if (maxStraightFlush > 0)
            {
                return (maxStraightFlush, WinHandType.STRAIGHT_FLUSH);
            }

            if (highestSameRank[3] >= 0)
            {
                return (highestSameRank[3], WinHandType.QUAD);
            }

            // For full house, encode the pair highest in the last 6 bits, and the trip highest in the next 6 bits
            if (highestSameRank[2] >= 0 && highestPairNotHighestTrip >= 0)
            {
                if (highestSameRank[1] != highestSameRank[2])
                {
                    highestPairNotHighestTrip = highestSameRank[1];
                }
                return ((highestSameRank[2] << 6) + highestPairNotHighestTrip, WinHandType.FULL_HOUSE);
            }

            // Flush. Manual loop unroll :0 Fuck the jit
            int maxFlush = -1;
            if (flushCount[0] >= 5 && orderingValueFlush[0] > maxFlush)
            {
                maxFlush = orderingValueFlush[0];
            }

            if (flushCount[1] >= 5 && orderingValueFlush[1] > maxFlush)
            {
                maxFlush = orderingValueFlush[1];
            }

            if (flushCount[2] >= 5 && orderingValueFlush[2] > maxFlush)
            {
                maxFlush = orderingValueFlush[2];
            }

            if (flushCount[3] >= 5 && orderingValueFlush[3] > maxFlush)
            {
                maxFlush = orderingValueFlush[3];
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

            // Trip
            if (highestSameRank[2] >= 0)
            {
                var tripCard = highestSameRank[2];
                orderingVal = (tripCard << 12);
                // Grab the other 2 highest card
                var rankCheck = GetRank(cards[^1]);
                if (rankCheck != tripCard)
                {
                    orderingVal += (rankCheck << 6);
                } else
                {
                    // Top card is trip, so grab the next 2
                    return (orderingVal 
                        + (GetRank(cards[^4]) << 6)
                        + GetRank(cards[^5])
                        , WinHandType.THREE_OF_A_KIND);
                }

                rankCheck = GetRank(cards[^2]);
                if (rankCheck != tripCard)
                {
                    orderingVal += rankCheck;
                } else
                {
                    // Second card is trip, so grab the next one
                    return (orderingVal
                        + GetRank(cards[^5])
                        , WinHandType.THREE_OF_A_KIND);
                }

                return (orderingVal, WinHandType.THREE_OF_A_KIND);
            }

            if (highestSameRank[1] >= 0)
            {
                int cardRank;
                if (highestPairNotHighestTrip >= 0)
                {
                    orderingVal = (highestSameRank[1] << 12) + (highestPairNotHighestTrip << 6);
                    cardRank = GetRank(cards[^1]);
                    if (cardRank != highestSameRank[1])
                    {
                        return (orderingVal + cardRank, WinHandType.TWO_PAIR);
                    }

                    // Highest 2 cards = highest pair, so check second pair
                    cardRank = GetRank(cards[^3]);
                    if (cardRank != highestPairNotHighestTrip)
                    {
                        return (orderingVal + cardRank, WinHandType.TWO_PAIR);
                    }

                    // If the other 2 matches, then the fifth card is the highest card
                    return (orderingVal + GetRank(cards[^5]), WinHandType.TWO_PAIR);
                    
                }

                // Pair
                var pairRank = highestSameRank[1];
                orderingVal = (pairRank << 18);
                cardRank = GetRank(cards[^1]);
                if (cardRank != pairRank)
                {
                    orderingVal += (cardRank << 12);
                } else
                {
                    // Top card is already top pair, so grab the next 3
                    return (orderingVal 
                        + (GetRank(cards[^3]) << 12)
                        + (GetRank(cards[^4]) << 6)
                        + GetRank(cards[^5])
                        , WinHandType.PAIR);
                }

                cardRank = GetRank(cards[^2]);
                if (cardRank != pairRank)
                {
                    orderingVal += (cardRank << 6);
                }
                else
                {
                    // Second card is top pair, grab the next 2
                    return (orderingVal
                        + (GetRank(cards[^4]) << 6)
                        + GetRank(cards[^5])
                        , WinHandType.PAIR);
                }

                cardRank = GetRank(cards[^3]);
                if (cardRank != pairRank)
                {
                    return (orderingVal + cardRank, WinHandType.PAIR);
                }

                // The third top card is the pair, so grab the fourth one
                return (orderingVal + GetRank(cards[^4]), WinHandType.PAIR);
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
