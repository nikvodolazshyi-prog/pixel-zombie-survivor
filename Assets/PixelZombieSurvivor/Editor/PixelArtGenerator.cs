using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelZombieSurvivor.Editor
{
    /// <summary>
    /// Generates the demo's pixel-art PNGs without external assets. Every sprite is
    /// imported as point-filtered, uncompressed pixel art.
    /// </summary>
    public static class PixelArtGenerator
    {
        public const string ArtFolder = "Assets/PixelZombieSurvivor/Art";

        private static readonly Dictionary<char, Color32> PlayerPalette = new Dictionary<char, Color32>
        {
            { 'h', Hex(0x3B2A1E) }, { 's', Hex(0xE4A56E) }, { 'e', Hex(0x1B1A19) },
            { 'j', Hex(0x3E7A4C) }, { 'J', Hex(0x285335) }, { 'p', Hex(0x2F4662) },
            { 'P', Hex(0x1F3045) }, { 'b', Hex(0x191B1F) }, { 'g', Hex(0x6F7882) }
        };

        private static readonly Dictionary<char, Color32> WalkerPalette = new Dictionary<char, Color32>
        {
            { 'z', Hex(0x74A84D) }, { 'Z', Hex(0x4F7D35) }, { 'e', Hex(0xFF4B3E) },
            { 'c', Hex(0x626B73) }, { 'C', Hex(0x454C53) }, { 'p', Hex(0x3C465C) },
            { 'b', Hex(0x1B1D21) }
        };

        private static readonly Dictionary<char, Color32> RunnerPalette = new Dictionary<char, Color32>
        {
            { 'z', Hex(0xB8C24D) }, { 'Z', Hex(0x87902C) }, { 'e', Hex(0xFFD23E) },
            { 'c', Hex(0x9C3E3E) }, { 'C', Hex(0x6B2929) }, { 'p', Hex(0x41314B) },
            { 'b', Hex(0x1B1D21) }
        };

        private static readonly Dictionary<char, Color32> BrutePalette = new Dictionary<char, Color32>
        {
            { 'z', Hex(0x6675B8) }, { 'Z', Hex(0x465286) }, { 'e', Hex(0xFF6A3D) },
            { 'c', Hex(0x422F2F) }, { 'C', Hex(0x2F2020) }, { 'p', Hex(0x322A2A) },
            { 'b', Hex(0x121316) }
        };

        private static readonly Dictionary<char, Color32> SpitterPalette = new Dictionary<char, Color32>
        {
            { 'z', Hex(0x9A62C8) }, { 'Z', Hex(0x6B3F94) }, { 'e', Hex(0xB6FF4B) },
            { 'c', Hex(0x4A3F5C) }, { 'C', Hex(0x332B42) }, { 'p', Hex(0x2C2438) },
            { 'b', Hex(0x141018) }
        };

        private static readonly string[] ZombieMap =
        {
            "................",
            "....zzzzzzzz....",
            "...zzzzzzzzzz...",
            "...zzezzzzezz...",
            "...zzzzzzzzzz...",
            "....zZZZZZZz....",
            "...cccccccccc...",
            "..ccCccccccCcc..",
            "..cccccccccccc..",
            "..cccccccccccc..",
            "...cccccccccc...",
            "...pppp..pppp...",
            "...pppp..pppp...",
            "...ppp....ppp...",
            "..bbbb....bbbb..",
            "................"
        };

        private static readonly string[] PlayerMap =
        {
            "................",
            "....hhhhhhhh....",
            "...hhhhhhhhhh...",
            "...hhsssssshh...",
            "...hssssssssh...",
            "...hseesseesh...",
            "....ssssssss....",
            "....jjjjjjjj....",
            "...jjjjjjjjjj...",
            "..sjjjjjjjjjjs..",
            "..sjjjjjjjjjjs..",
            "...jjjjjjjjjj...",
            "...pppppppppp...",
            "...ppp....ppp...",
            "...ppp....ppp...",
            "..bbbb....bbbb.."
        };

        // Legs occupy the last three rows for the player and the last four for
        // zombies. Each variant is a horizontal stride pose; frames combine them
        // with a subtle upper-body sway.
        private static readonly string[][] PlayerLegVariants =
        {
            new[] { "...ppp....ppp...", "...ppp....ppp...", "..bbbb....bbbb.." },
            new[] { "....ppp...ppp...", "....ppp...ppp...", "...bbbb...bbbb.." },
            new[] { "..ppp.....ppp...", "...ppp....ppp...", "..bbbb....bbbb.." },
            new[] { "...ppp...ppp....", "...ppp...ppp....", "..bbbb...bbbb..." }
        };

        private static readonly string[][] ZombieLegVariants =
        {
            new[] { "...pppp..pppp...", "...pppp..pppp...", "...ppp....ppp...", "..bbbb....bbbb.." },
            new[] { "....pppp.pppp...", "....pppp.pppp...", "....ppp...ppp...", "...bbbb...bbbb.." },
            new[] { "..pppp...pppp...", "..pppp...pppp...", "...ppp....ppp...", "..bbbb....bbbb.." },
            new[] { "...pppp.pppp....", "...pppp.pppp....", "...ppp...ppp....", "..bbbb...bbbb..." }
        };

        private static readonly int[] WalkSway = { 0, 1, 0, -1, 0, 1, 0, -1 };

        private static readonly string[] PistolMap =
        {
            "..............",
            "....gggggggg..",
            "...gGGGGGGGGg.",
            "..hGGGGGGGGGGg",
            "..h.....gggg..",
            "..h...........",
            "..............",
            ".............."
        };

        private static readonly string[] SmgMap =
        {
            "................",
            "....gggggggggg..",
            "...gGGGGGGGGGGg.",
            "..hGGGGGGGGGGGGg",
            "..h....gg...gg..",
            "..h.............",
            "................",
            "................"
        };

        private static readonly string[] AkMap =
        {
            "..................",
            "....gggggggggggg..",
            "...gGGGGGGGGGGGGg.",
            "..wwGGGGGGGGGGGGg.",
            "..ww...ww...gg....",
            "..ww..............",
            "..................",
            ".................."
        };

        private static readonly string[] M4Map =
        {
            "..................",
            "....gggggggggggg..",
            "...gGGGGGGGGGGGGg.",
            "..ttGGGGGGGGGGGGg.",
            "..tt...tt...gg....",
            "..tt..............",
            "..................",
            ".................."
        };

        private static readonly string[] LmgMap =
        {
            "....................",
            "....gggggggggggggg..",
            "...gGGGGGGGGGGGGGGg.",
            "..hGGGGGGGGGGGGGGGGg",
            "..h....gg....gg.....",
            "..h..............g..",
            "....................",
            "....................",
            "...................."
        };

        private static readonly string[] ShotgunMap =
        {
            "..................",
            "....gggggggggggg..",
            "...gGGGGGGGGGGGGg.",
            "..hGGGGGGGGGGGGGGg",
            "..h...gg...gg.....",
            "..h...............",
            "..................",
            ".................."
        };

        private static readonly string[] BarrelMap =
        {
            "................",
            "....ggggggg.....",
            "...gGGGGGGGg....",
            "..hhGGGGGGGGGgg.",
            "..hh...gggg.....",
            "................"
        };

        private static readonly string[] CoinMap =
        {
            "..gggg..",
            ".gYYYYg.",
            "gYYWWYYg",
            "gYWWWWYg",
            "gYWWWWYg",
            "gYYWWYYg",
            ".gYYYYg.",
            "..gggg.."
        };

        private static readonly string[] BulletMap =
        {
            "..YYY.",
            "yyyyyy"
        };

        private static readonly string[] ShadowMap =
        {
            "....dddd....",
            "..dddddddd..",
            "..dddddddd..",
            "....dddd...."
        };

        private static readonly Dictionary<char, Color32> WeaponPalette = new Dictionary<char, Color32>
        {
            { 'g', Hex(0x59636E) }, { 'G', Hex(0x8D99A5) }, { 'h', Hex(0x23282E) },
            { 'w', Hex(0x8B5A2B) }, { 't', Hex(0x9C8B6B) }
        };

        private static readonly Dictionary<char, Color32> CoinPalette = new Dictionary<char, Color32>
        {
            { 'g', Hex(0xB67A1C) }, { 'Y', Hex(0xFFD05A) }, { 'W', Hex(0xFFF3B0) }
        };

        private static readonly Dictionary<char, Color32> BulletPalette = new Dictionary<char, Color32>
        {
            { 'Y', Hex(0xFFF27A) }, { 'y', Hex(0xFFB52E) }
        };

        private static readonly Dictionary<char, Color32> ShadowPalette = new Dictionary<char, Color32>
        {
            { 'd', new Color32(0, 0, 0, 90) }
        };

        public static Dictionary<string, Sprite> GenerateAll()
        {
            EnsureFolder(ArtFolder);

            Sprite[] playerIdle = SaveIdleFrames("player_idle", PlayerMap, PlayerPalette, 13, PlayerLegVariants);
            Sprite[] playerWalk = SaveWalkFrames("player_walk", PlayerMap, PlayerPalette, 13, PlayerLegVariants);
            Sprite[] walkerWalk = SaveWalkFrames("zombie_walker_walk", ZombieMap, WalkerPalette, 11, ZombieLegVariants);
            Sprite[] walkerAttack = SaveAttackFrames("zombie_walker_attack", ZombieMap, WalkerPalette, 11, ZombieLegVariants, 8);
            Sprite[] runnerWalk = SaveWalkFrames("zombie_runner_walk", ZombieMap, RunnerPalette, 11, ZombieLegVariants);
            Sprite[] runnerAttack = SaveAttackFrames("zombie_runner_attack", ZombieMap, RunnerPalette, 11, ZombieLegVariants, 8);
            Sprite[] bruteWalk = SaveWalkFrames("zombie_brute_walk", ZombieMap, BrutePalette, 11, ZombieLegVariants);
            Sprite[] bruteAttack = SaveAttackFrames("zombie_brute_attack", ZombieMap, BrutePalette, 11, ZombieLegVariants, 8);
            Sprite[] spitterWalk = SaveWalkFrames("zombie_spitter_walk", ZombieMap, SpitterPalette, 11, ZombieLegVariants);
            Sprite[] spitterAttack = SaveAttackFrames("zombie_spitter_attack", ZombieMap, SpitterPalette, 11, ZombieLegVariants, 8);

            Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>
            {
                ["player"] = SaveMap("player", PlayerMap, PlayerPalette, 16f, new Vector2(0.5f, 0.5f)),
                ["player_shadow"] = SaveMap("player_shadow", ShadowMap, ShadowPalette, 16f, new Vector2(0.5f, 0.5f)),
                ["zombie_walker"] = SaveMap("zombie_walker", ZombieMap, WalkerPalette, 16f, new Vector2(0.5f, 0.5f)),
                ["zombie_runner"] = SaveMap("zombie_runner", ZombieMap, RunnerPalette, 16f, new Vector2(0.5f, 0.5f)),
                ["zombie_brute"] = SaveMap("zombie_brute", ZombieMap, BrutePalette, 16f, new Vector2(0.5f, 0.5f)),
                ["zombie_spitter"] = SaveMap("zombie_spitter", ZombieMap, SpitterPalette, 16f, new Vector2(0.5f, 0.5f)),
                ["weapon_pistol"] = SaveMap("weapon_pistol", PistolMap, WeaponPalette, 16f, new Vector2(0.18f, 0.45f)),
                ["weapon_smg"] = SaveMap("weapon_smg", SmgMap, WeaponPalette, 16f, new Vector2(0.16f, 0.45f)),
                ["weapon_ak47"] = SaveMap("weapon_ak47", AkMap, WeaponPalette, 16f, new Vector2(0.14f, 0.45f)),
                ["weapon_m4a1"] = SaveMap("weapon_m4a1", M4Map, WeaponPalette, 16f, new Vector2(0.14f, 0.45f)),
                ["weapon_lmg"] = SaveMap("weapon_lmg", LmgMap, WeaponPalette, 16f, new Vector2(0.12f, 0.45f)),
                ["weapon_shotgun"] = SaveMap("weapon_shotgun", ShotgunMap, WeaponPalette, 16f, new Vector2(0.14f, 0.45f)),
                ["turret_barrel"] = SaveMap("turret_barrel", BarrelMap, WeaponPalette, 16f, new Vector2(0.16f, 0.5f)),
                ["coin"] = SaveMap("coin", CoinMap, CoinPalette, 16f, new Vector2(0.5f, 0.5f)),
                ["bullet"] = SaveMap("bullet", BulletMap, BulletPalette, 16f, new Vector2(0.5f, 0.5f)),
                ["ground"] = SaveFloor(),
                ["wall"] = SaveWall(),
                ["turret_base"] = SaveTurretBase(),
                ["turret_heavy"] = SaveHeavyTurret(),
                ["boss"] = SaveBoss(),
                ["money_generator"] = SaveGenerator(),
                ["repair_station"] = SaveRepairStation(),
                ["health_pack"] = SaveHealthPack(),
                ["ammo_box"] = SaveAmmoBox()
            };

            AddFrames(sprites, "player_idle", playerIdle);
            AddFrames(sprites, "player_walk", playerWalk);
            AddFrames(sprites, "zombie_walker_walk", walkerWalk);
            AddFrames(sprites, "zombie_walker_attack", walkerAttack);
            AddFrames(sprites, "zombie_runner_walk", runnerWalk);
            AddFrames(sprites, "zombie_runner_attack", runnerAttack);
            AddFrames(sprites, "zombie_brute_walk", bruteWalk);
            AddFrames(sprites, "zombie_brute_attack", bruteAttack);
            AddFrames(sprites, "zombie_spitter_walk", spitterWalk);
            AddFrames(sprites, "zombie_spitter_attack", spitterAttack);

            AssetDatabase.SaveAssets();
            return sprites;
        }

        private static void AddFrames(Dictionary<string, Sprite> sprites, string prefix, Sprite[] frames)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                sprites[$"{prefix}_{i}"] = frames[i];
            }
        }

        private static Sprite[] SaveWalkFrames(
            string prefix,
            string[] map,
            Dictionary<char, Color32> palette,
            int legRowStart,
            string[][] legVariants)
        {
            Sprite[] frames = new Sprite[8];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = SaveFrame(
                    $"{prefix}_{i}",
                    map,
                    palette,
                    legRowStart,
                    legVariants[i % legVariants.Length],
                    WalkSway[i % WalkSway.Length],
                    false,
                    0);
            }

            return frames;
        }

        private static Sprite[] SaveIdleFrames(
            string prefix,
            string[] map,
            Dictionary<char, Color32> palette,
            int legRowStart,
            string[][] legVariants)
        {
            Sprite[] frames = new Sprite[2];
            for (int i = 0; i < frames.Length; i++)
            {
                // Idle keeps the legs planted and only sways the upper body, which
                // reads as breathing rather than as a small step.
                frames[i] = SaveFrame(
                    $"{prefix}_{i}",
                    map,
                    palette,
                    legRowStart,
                    legVariants[0],
                    i,
                    false,
                    0);
            }

            return frames;
        }

        private static Sprite[] SaveAttackFrames(
            string prefix,
            string[] map,
            Dictionary<char, Color32> palette,
            int legRowStart,
            string[][] legVariants,
            int armRowStart)
        {
            int[] lean = { 1, 2, 2, 1, 0, 0 };
            bool[] reach = { false, true, true, true, false, false };
            Sprite[] frames = new Sprite[lean.Length];

            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = SaveFrame(
                    $"{prefix}_{i}",
                    map,
                    palette,
                    legRowStart,
                    legVariants[0],
                    lean[i],
                    reach[i],
                    armRowStart);
            }

            return frames;
        }

        private static Sprite SaveFrame(
            string name,
            string[] map,
            Dictionary<char, Color32> palette,
            int legRowStart,
            string[] legs,
            int upperOffsetX,
            bool attackReach,
            int armRowStart)
        {
            int width = map[0].Length;
            int height = map.Length;
            ValidateRows(name, map, width);
            ValidateRows(name, legs, width);

            Texture2D texture = NewTexture(width, height);
            DrawRows(texture, map, palette, 0, upperOffsetX, width, height, legRowStart);
            DrawRows(texture, legs, palette, legRowStart, 0, width, height);

            if (attackReach)
            {
                Color32 armColor = palette.TryGetValue('c', out Color32 color) ? color : Color.white;
                for (int row = armRowStart; row < armRowStart + 2; row++)
                {
                    for (int x = width - 2; x < width; x++)
                    {
                        texture.SetPixel(x, height - 1 - row, armColor);
                    }
                }
            }

            return SaveTexture(name, texture, 16f, new Vector2(0.5f, 0.5f), TextureWrapMode.Clamp);
        }

        private static void DrawRows(
            Texture2D texture,
            string[] rows,
            Dictionary<char, Color32> palette,
            int startRow,
            int offsetX,
            int width,
            int height,
            int rowLimit = int.MaxValue)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                string row = rows[i];
                int textureRow = startRow + i;
                if (textureRow >= height || textureRow >= rowLimit)
                {
                    break;
                }

                for (int x = 0; x < row.Length; x++)
                {
                    char key = row[x];
                    if (key == '.' || key == ' ')
                    {
                        continue;
                    }

                    if (!palette.TryGetValue(key, out Color32 color))
                    {
                        continue;
                    }

                    int px = x + offsetX;
                    if (px < 0 || px >= width)
                    {
                        continue;
                    }

                    texture.SetPixel(px, height - 1 - textureRow, color);
                }
            }
        }

        private static void ValidateRows(string name, string[] rows, int width)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].Length != width)
                {
                    throw new IOException(
                        $"Pixel map '{name}' row {i} has length {rows[i].Length}, expected {width}.");
                }
            }
        }

        private static Sprite SaveFloor()
        {
            const int size = 16;
            Texture2D texture = NewTexture(size, size);
            Color32 baseColor = Hex(0x141B20);
            Color32 grain = Hex(0x1B242A);
            Color32 bright = Hex(0x223038);
            Color32 seam = Hex(0x0D1215);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color32 color = baseColor;
                    if ((x * 17 + y * 31) % 29 == 0) color = grain;
                    if ((x * 7 + y * 13) % 53 == 0) color = bright;
                    if (x == 0 || y == 0) color = seam;
                    texture.SetPixel(x, y, color);
                }
            }

            return SaveTexture("ground", texture, 16f, new Vector2(0.5f, 0.5f), TextureWrapMode.Repeat);
        }

        private static Sprite SaveWall()
        {
            const int size = 16;
            Texture2D texture = NewTexture(size, size);
            Color32 dark = Hex(0x1C2328);
            Color32 mid = Hex(0x303B40);
            Color32 light = Hex(0x4A575C);
            Color32 rivet = Hex(0x76868B);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color32 color = mid;
                    if (y == 0 || y == 15 || x == 0 || x == 15) color = dark;
                    if (y == 1) color = light;
                    if (y == 14) color = dark;
                    if ((x == 3 || x == 12) && (y == 3 || y == 12)) color = rivet;
                    texture.SetPixel(x, y, color);
                }
            }

            return SaveTexture("wall", texture, 16f, new Vector2(0.5f, 0.5f), TextureWrapMode.Repeat);
        }

        private static Sprite SaveTurretBase()
        {
            const int size = 20;
            Texture2D texture = NewTexture(size, size);
            Vector2 center = new Vector2(9.5f, 9.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    Color32? color = null;
                    if (distance <= 9f) color = Hex(0x242B31);
                    if (distance <= 7.8f) color = Hex(0x4B575F);
                    if (distance <= 6.2f) color = Hex(0x303940);
                    if (distance <= 3.2f) color = Hex(0xB77A2A);

                    if (color.HasValue)
                    {
                        texture.SetPixel(x, y, color.Value);
                    }
                }
            }

            int[,] bolts = { { 4, 4 }, { 15, 4 }, { 4, 15 }, { 15, 15 } };
            for (int i = 0; i < bolts.GetLength(0); i++)
            {
                texture.SetPixel(bolts[i, 0], bolts[i, 1], Hex(0xA8B6BC));
                texture.SetPixel(bolts[i, 0] + 1, bolts[i, 1], Hex(0x66757B));
            }

            return SaveTexture("turret_base", texture, 16f, new Vector2(0.5f, 0.5f), TextureWrapMode.Clamp);
        }

        private static Sprite SaveHeavyTurret()
        {
            const int size = 24;
            Texture2D texture = NewTexture(size, size);
            Vector2 center = new Vector2(11.5f, 11.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    Color32? color = null;
                    if (distance <= 11.2f) color = Hex(0x1B2026);
                    if (distance <= 10f) color = Hex(0x3B4650);
                    if (distance <= 8.2f) color = Hex(0x59656D);
                    if (distance <= 6.4f) color = Hex(0x262D34);
                    if (distance <= 4.4f) color = Hex(0xC2452E);
                    if (distance <= 2.4f) color = Hex(0xFF7A3D);

                    if (color.HasValue)
                    {
                        texture.SetPixel(x, y, color.Value);
                    }
                }
            }

            int[,] vents = { { 3, 8 }, { 3, 15 }, { 20, 8 }, { 20, 15 } };
            for (int i = 0; i < vents.GetLength(0); i++)
            {
                int vx = vents[i, 0];
                int vy = vents[i, 1];
                texture.SetPixel(vx, vy, Hex(0xE8A15A));
                texture.SetPixel(vx + 1, vy, Hex(0x8A5A2A));
            }

            return SaveTexture("turret_heavy", texture, 16f, new Vector2(0.5f, 0.5f), TextureWrapMode.Clamp);
        }

        private static Sprite SaveBoss()
        {
            const int size = 32;
            Texture2D texture = NewTexture(size, size);
            Color32 outline = Hex(0x14090A);
            Color32 body = Hex(0x6B1F1F);
            Color32 bodyLight = Hex(0x9A2B2B);
            Color32 bodyDark = Hex(0x431313);
            Color32 gold = Hex(0xE6A23C);
            Color32 eye = Hex(0xC7FF4A);
            Color32 tooth = Hex(0xE8E0D0);

            for (int y = 4; y <= 27; y++)
            {
                for (int x = 5; x <= 26; x++)
                {
                    texture.SetPixel(x, y, outline);
                }
            }

            for (int y = 8; y <= 24; y++)
            {
                for (int x = 6; x <= 25; x++)
                {
                    texture.SetPixel(x, y, body);
                }
            }

            for (int y = 10; y <= 22; y++)
            {
                for (int x = 8; x <= 14; x++)
                {
                    texture.SetPixel(x, y, bodyLight);
                }
            }

            for (int y = 6; y <= 8; y++)
            {
                for (int x = 7; x <= 24; x++)
                {
                    texture.SetPixel(x, y, bodyDark);
                }
            }

            // Head and jaw.
            for (int y = 24; y <= 30; y++)
            {
                for (int x = 10; x <= 21; x++)
                {
                    texture.SetPixel(x, y, y >= 29 ? outline : bodyDark);
                }
            }

            // Horns.
            for (int y = 28; y <= 31; y++)
            {
                for (int x = 7; x <= 9; x++) texture.SetPixel(x, y, gold);
                for (int x = 22; x <= 24; x++) texture.SetPixel(x, y, gold);
            }

            // Shoulder spikes and arms.
            for (int y = 17; y <= 24; y++)
            {
                for (int x = 2; x <= 6; x++) texture.SetPixel(x, y, bodyDark);
                for (int x = 25; x <= 29; x++) texture.SetPixel(x, y, bodyDark);
            }

            for (int y = 9; y <= 18; y++)
            {
                for (int x = 3; x <= 5; x++) texture.SetPixel(x, y, body);
                for (int x = 26; x <= 28; x++) texture.SetPixel(x, y, body);
            }

            // Legs.
            for (int y = 1; y <= 7; y++)
            {
                for (int x = 8; x <= 13; x++) texture.SetPixel(x, y, bodyDark);
                for (int x = 18; x <= 23; x++) texture.SetPixel(x, y, bodyDark);
            }

            // Eyes, teeth, belt.
            for (int x = 12; x <= 14; x++) texture.SetPixel(x, 27, eye);
            for (int x = 17; x <= 19; x++) texture.SetPixel(x, 27, eye);
            for (int x = 12; x <= 19; x += 2) texture.SetPixel(x, 25, tooth);
            for (int x = 7; x <= 24; x++) texture.SetPixel(x, 9, gold);

            return SaveTexture("boss", texture, 16f, new Vector2(0.5f, 0.5f), TextureWrapMode.Clamp);
        }

        private static Sprite SaveGenerator()
        {
            const int size = 20;
            Texture2D texture = NewTexture(size, size);
            Color32 dark = Hex(0x183024);
            Color32 body = Hex(0x2F6B47);
            Color32 light = Hex(0x4B9867);
            Color32 gold = Hex(0xFFD05A);
            Color32 goldDark = Hex(0xB67A1C);

            for (int y = 2; y < 18; y++)
            {
                for (int x = 2; x < 18; x++)
                {
                    Color32 color = body;
                    if (x == 2 || y == 2 || x == 17 || y == 17) color = dark;
                    if (y == 3 || x == 3) color = light;
                    if ((x == 5 || x == 14) && (y == 5 || y == 14)) color = Hex(0x9DBBAA);
                    texture.SetPixel(x, y, color);
                }
            }

            Vector2 center = new Vector2(9.5f, 9.5f);
            for (int y = 5; y < 15; y++)
            {
                for (int x = 5; x < 15; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance <= 3.8f) texture.SetPixel(x, y, goldDark);
                    if (distance <= 3.1f) texture.SetPixel(x, y, gold);
                    if (distance <= 1.7f) texture.SetPixel(x, y, goldDark);
                }
            }

            return SaveTexture("money_generator", texture, 16f, new Vector2(0.5f, 0.5f), TextureWrapMode.Clamp);
        }

        private static Sprite SaveRepairStation()
        {
            const int size = 20;
            Texture2D texture = NewTexture(size, size);
            Color32 dark = Hex(0x252C32);
            Color32 body = Hex(0x59656D);
            Color32 light = Hex(0x7F8D95);
            Color32 panel = Hex(0xD7DEE2);
            Color32 red = Hex(0xD94242);

            for (int y = 2; y < 18; y++)
            {
                for (int x = 2; x < 18; x++)
                {
                    Color32 color = body;
                    if (x == 2 || y == 2 || x == 17 || y == 17) color = dark;
                    if (y == 3 || x == 3) color = light;
                    if (x >= 5 && x <= 14 && y >= 5 && y <= 14) color = panel;
                    texture.SetPixel(x, y, color);
                }
            }

            for (int y = 7; y <= 12; y++)
            {
                for (int x = 9; x <= 10; x++) texture.SetPixel(x, y, red);
            }

            for (int y = 9; y <= 10; y++)
            {
                for (int x = 7; x <= 12; x++) texture.SetPixel(x, y, red);
            }

            return SaveTexture("repair_station", texture, 16f, new Vector2(0.5f, 0.5f), TextureWrapMode.Clamp);
        }

        private static Sprite SaveHealthPack()
        {
            const int size = 16;
            Texture2D texture = NewTexture(size, size);
            Color32 white = Hex(0xE8EFF2);
            Color32 shade = Hex(0xAAB8BE);
            Color32 red = Hex(0xD94242);
            Color32 dark = Hex(0x293137);

            for (int y = 3; y < 14; y++)
            {
                for (int x = 2; x < 14; x++)
                {
                    Color32 color = white;
                    if (x == 2 || x == 13 || y == 3 || y == 13) color = dark;
                    if (x == 3 || y == 4) color = shade;
                    texture.SetPixel(x, y, color);
                }
            }

            for (int y = 6; y <= 11; y++)
            {
                for (int x = 7; x <= 8; x++) texture.SetPixel(x, y, red);
            }

            for (int y = 8; y <= 9; y++)
            {
                for (int x = 5; x <= 10; x++) texture.SetPixel(x, y, red);
            }

            return SaveTexture("health_pack", texture, 16f, new Vector2(0.5f, 0.5f), TextureWrapMode.Clamp);
        }

        private static Sprite SaveAmmoBox()
        {
            const int size = 16;
            Texture2D texture = NewTexture(size, size);
            Color32 dark = Hex(0x2A241B);
            Color32 body = Hex(0x846A38);
            Color32 light = Hex(0xB08F4D);
            Color32 brass = Hex(0xE6B84B);

            for (int y = 4; y < 14; y++)
            {
                for (int x = 2; x < 14; x++)
                {
                    Color32 color = body;
                    if (x == 2 || x == 13 || y == 4 || y == 13) color = dark;
                    if (y == 5 || x == 3) color = light;
                    texture.SetPixel(x, y, color);
                }
            }

            for (int i = 0; i < 4; i++)
            {
                int x = 4 + i * 2;
                texture.SetPixel(x, 7, brass);
                texture.SetPixel(x, 8, brass);
                texture.SetPixel(x, 9, Hex(0xB77E2A));
            }

            return SaveTexture("ammo_box", texture, 16f, new Vector2(0.5f, 0.5f), TextureWrapMode.Clamp);
        }

        private static Sprite SaveMap(
            string name,
            string[] map,
            Dictionary<char, Color32> palette,
            float pixelsPerUnit,
            Vector2 pivot)
        {
            int width = map[0].Length;
            int height = map.Length;
            for (int y = 0; y < height; y++)
            {
                if (map[y].Length != width)
                {
                    throw new IOException($"Pixel map '{name}' row {y} has length {map[y].Length}, expected {width}.");
                }
            }

            Texture2D texture = NewTexture(width, height);
            for (int y = 0; y < height; y++)
            {
                string row = map[y];
                for (int x = 0; x < width; x++)
                {
                    char key = row[x];
                    if (key == '.' || key == ' ')
                    {
                        continue;
                    }

                    if (!palette.TryGetValue(key, out Color32 color))
                    {
                        throw new IOException($"Pixel map '{name}' uses unknown palette key '{key}'.");
                    }

                    texture.SetPixel(x, height - 1 - y, color);
                }
            }

            return SaveTexture(name, texture, pixelsPerUnit, pivot, TextureWrapMode.Clamp);
        }

        private static Sprite SaveTexture(
            string name,
            Texture2D texture,
            float pixelsPerUnit,
            Vector2 pivot,
            TextureWrapMode wrapMode)
        {
            texture.name = name;
            texture.Apply();

            string assetPath = $"{ArtFolder}/{name}.png";
            string absolutePath = Path.GetFullPath(assetPath);
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = wrapMode;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;

                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                settings.spriteExtrude = 0;
                settings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Texture2D NewTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] clear = new Color32[width * height];
            texture.SetPixels32(clear);
            return texture;
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static Color32 Hex(int value)
        {
            return new Color32(
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF),
                255);
        }
    }
}
