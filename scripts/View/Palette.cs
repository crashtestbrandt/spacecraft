// Per-player colors and the pixel maps the ship views draw. Sprites are authored facing up (-y).
using global::Godot;

namespace Spacecraft.View
{
    public static class Palette
    {
        public static readonly Color[] Player =
        {
            new Color(0.35f, 0.75f, 1.00f),   // P1 blue
            new Color(1.00f, 0.42f, 0.36f),   // P2 red
            new Color(0.45f, 1.00f, 0.50f),   // P3 green
            new Color(1.00f, 0.85f, 0.35f),   // P4 yellow
        };

        public static Color Of(int playerId) => Player[(playerId & 3)];

        // Pixel map legend: '#' body (player color), '+' highlight, 'o' cockpit, '.' empty.
        public static readonly string[] Fighter =
        {
            "....#....",
            "....#....",
            "...+#+...",
            "..+###+..",
            ".#+###+#.",
            ".#.###.#.",
            "##.#o#.##",
            "#..#.#..#",
            "#.......#",
        };

        public static readonly string[] Flagship =
        {
            ".....#.#.....",
            "....##.##....",
            "...#######...",
            "..##+###+##..",
            ".#####o#####.",
            "######o######",
            "#.###+#+###.#",
            "#..##...##..#",
            "#..#.....#..#",
            "....#...#....",
            "...##...##...",
        };

        public static string[] MapFor(int kind) => kind == 0 ? Flagship : Fighter;

        public static Color Shade(Color body, char c)
        {
            switch (c)
            {
                case '+': return body.Lightened(0.45f);
                case 'o': return new Color(1f, 1f, 1f);
                default:  return body;
            }
        }
    }
}
