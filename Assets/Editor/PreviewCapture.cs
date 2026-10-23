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
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "-previewOut" && i + 1 < args.Length)
                output = args[i + 1];
            if (args[i] == "-previewPlaceArmy")
                placeArmy = true;
            if (args[i] == "-previewEncounter" && i + 1 < args.Length)
                int.TryParse(args[i + 1], out encounter);
        }

        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        Capture(output, placeArmy, encounter);
        EditorApplication.Exit(0);
    }

    private static void Capture(string output, bool placeArmy, int encounter = 0)
    {
        var encounters = new[] { Encounter.Wolves, Encounter.Necromancer, Encounter.Ogre, Encounter.Goblins };
        var army = ArmyMember.StartingArmy();
        army.Add(new ArmyMember(UnitStats.Sapper));
        var battle = encounters[Mathf.Clamp(encounter, 0, encounters.Length - 1)].CreateBattle(army);
        if (placeArmy)
        {
            var x = 2;
            foreach (var u in battle.Units)
                if (u.Team == Team.Player)
                    battle.Place(u, new GridPos(x++, 1));
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
            foreach (var p in battle.BlastArea(bomb.Tile))
                board.SetHighlight(p, new Color(1f, 0.45f, 0.1f, 0.4f));
            var cam = Camera.main;
            GameBootstrap.FrameCamera(cam, board);
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
            RenderTexture.active = null;
            Debug.Log($"Battle preview written to {Path.GetFullPath(output)}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
