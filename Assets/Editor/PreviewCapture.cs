using System.IO;
using System.Linq;
using ArmyRoguelike.Core;
using ArmyRoguelike.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Dev tool: renders the prototype battle to a PNG without entering Play mode.
/// Batch mode: -executeMethod PreviewCapture.CaptureFromCommandLine -previewOut path.png
/// </summary>
public static class PreviewCapture
{
    [MenuItem("Tools/Capture Battle Preview")]
    private static void CaptureFromMenu() => Capture(Path.Combine(Application.dataPath, "../Temp/battle-preview.png"), false);

    public static void CaptureFromCommandLine()
    {
        var args = System.Environment.GetCommandLineArgs();
        var output = "battle-preview.png";
        var placeArmy = false;
        var encounter = 0;
        var palette = BattleScenery.MainPalette;
        var setting = BattleScenery.MainSetting;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "-previewOut" && i + 1 < args.Length)
                output = args[i + 1];
            if (args[i] == "-previewPlaceArmy")
                placeArmy = true;
            if (args[i] == "-previewEncounter" && i + 1 < args.Length)
                int.TryParse(args[i + 1], out encounter);
            if (args[i] == "-previewPalette" && i + 1 < args.Length)
                int.TryParse(args[i + 1], out palette);
            // -previewSetting TowerSky|IsoTower|IsoTemple|IsoGraveyard
            if (args[i] == "-previewSetting" && i + 1 < args.Length)
                System.Enum.TryParse(args[i + 1], out setting);
        }

        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        Capture(output, placeArmy, encounter, palette, args.Contains("-previewPixelate"), setting);
        EditorApplication.Exit(0);
    }

    internal static void Capture(string output, bool placeArmy, int encounter = 0, int palette = BattleScenery.MainPalette, bool pixelate = false,
        BattleScenery.Setting setting = BattleScenery.MainSetting, System.Action<BoardView> decorate = null)
    {
        // -previewEncounter: 0 wolves, 1 goblins, 2 bandits, 3 elementals, 4 necromancer, 5 giant.
        var encounters = Encounter.Regular.Concat(Encounter.Bosses).ToArray();
        var army = ArmyMember.StartingArmy();
        army.Add(new ArmyMember(UnitStats.Sapper));

        // -previewUnits "Juggernaut,Sniper,...": show these unit types instead (up to 16, filling the back two rows).
        var args = System.Environment.GetCommandLineArgs();
        var unitsArg = System.Array.IndexOf(args, "-previewUnits");
        if (unitsArg >= 0 && unitsArg + 1 < args.Length)
        {
            var names = args[unitsArg + 1].Split(',').Select(n => n.Trim());
            army = names.Select(n => UnitStats.AllPlayerTypes.Concat(new[] { UnitStats.Turret }).FirstOrDefault(s => s.Name == n))
                .Where(s => s != null).Take(16).Select(s => new ArmyMember(s)).ToList();
        }

        var battle = encounters[Mathf.Clamp(encounter, 0, encounters.Length - 1)].CreateBattle(army);
        if (placeArmy)
        {
            var i = 0;
            foreach (var u in battle.Units.Where(u => u.Team == Team.Player))
            {
                // Front row first, from the left; small armies sit in the middle as before.
                var slot = army.Count <= 6 ? new GridPos(2 + i, 1) : new GridPos(i % 8, 1 - i / 8);
                battle.Place(u, slot);
                i++;
            }
        }

        // -previewPlayTurn: the sapper lays a mine and the enemies take one turn, to show mines and bombs.
        var playTurn = System.Environment.GetCommandLineArgs().Contains("-previewPlayTurn");
        if (placeArmy && playTurn && battle.StartBattle())
        {
            var sapper = battle.Units.FirstOrDefault(u => u.Has(Ability.Mines));
            if (sapper != null)
                battle.PlayerPlaceMine(sapper, new GridPos(sapper.Pos.X, sapper.Pos.Y + 1));
            battle.EndPlayerTurn();
        }

        var root = new GameObject("PreviewRoot");
        try
        {
            var board = BoardView.Create(battle, root.transform);
            board.SyncHazards();
            foreach (var bomb in battle.Bombs)
            foreach (var p in battle.BombArea(bomb.Tile))
                board.SetHighlight(p, new Color(1f, 0.45f, 0.1f, 0.4f));
            var cam = Camera.main;
            GameBootstrap.FrameCamera(cam, board);
            // The capture is 16:9; frame the scenery for that, whatever the batch-mode camera thinks.
            cam.aspect = 16f / 9f;
            BattleScenery.Apply(cam, board, palette, pixelate, setting);
            // Optional extras for experiment renders, e.g. highlights as if a unit were selected.
            decorate?.Invoke(board);
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            // The first render after creating sprites can miss their textures; the second is reliable.
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(output, tex.EncodeToPNG());
            cam.targetTexture = null;
            cam.ResetAspect();
            RenderTexture.active = null;
            Debug.Log($"Battle preview written to {Path.GetFullPath(output)}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
