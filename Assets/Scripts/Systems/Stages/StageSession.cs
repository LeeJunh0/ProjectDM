using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectDM
{
    /// <summary>Owns stage selection and applies the biome once at the next run boundary.</summary>
    internal sealed class StageSession
    {
        private readonly GameSettings settings;
        private readonly WorldArtAssets art;
        private readonly Dictionary<int, StageDefinition> definitions = new();
        private int configuredId;
        internal int SelectedId { get; private set; }
        internal StageDefinition Current { get; private set; }
        internal bool SelectionChanged => Current != null && Current.stageId != SelectedId;
        internal bool Ready => definitions.Count > 0;

        internal StageSession(GameSettings settings, WorldArtAssets art)
        {
            this.settings = settings;
            this.art = art;
            configuredId = SelectedId = Mathf.Clamp(settings.initialStage, 1, 2);
        }

        internal void LoadContent() => SetDefinitions(Resources.LoadAll<StageDefinition>("ProjectDM/Stages"));

        internal void SetDefinitions(StageDefinition[] stages)
        {
            if (stages == null || stages.Length != 2) throw new InvalidOperationException("1·2 스테이지 리소스가 필요합니다. Project DM/Import Stage Content를 실행하세요.");
            definitions.Clear();
            foreach (StageDefinition stage in stages)
            {
                if (stage == null) throw new InvalidOperationException("스테이지 리소스가 누락되었습니다.");
                stage.ValidateContent();
                if (stage.stageId < 1 || stage.stageId > 2 || definitions.ContainsKey(stage.stageId))
                    throw new InvalidOperationException("스테이지 번호는 1·2로 중복 없이 지정하세요.");
                definitions.Add(stage.stageId, stage);
            }
        }

        internal StageDefinition Definition(int id) => definitions.TryGetValue(id, out StageDefinition stage) ? stage : null;
        internal bool Select(int id)
        {
            if (!definitions.ContainsKey(id)) return false;
            SelectedId = id;
            return true;
        }

        internal void ApplySelected()
        {
            if (!Ready) return; // In-memory legacy regression fixtures have no imported artwork.
            int requested = Mathf.Clamp(settings.initialStage, 1, 2);
            if (configuredId != requested) { configuredId = requested; SelectedId = requested; }
            Current = definitions[SelectedId];
            art.stage = Current;
            if (settings.dungeonFloor == null)
            {
                GameObject grid = new GameObject("Stage Ground", typeof(Grid));
                GameObject floor = new GameObject("Stage Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
                floor.transform.SetParent(grid.transform, false);
                settings.dungeonFloor = floor.AddComponent<DungeonFloorTilemap>();
                floor.GetComponent<TilemapRenderer>().sortingOrder = -10;
            }
            settings.dungeonFloor.SetGroundRuleTile(Current.ground);
            settings.dungeonFloor.Configure(settings.fieldBounds != null ? settings.fieldBounds.Size : settings.fieldSize);
            var scenery = settings.dungeonFloor.GetComponent<StageSceneryRenderer>();
            if (scenery == null) scenery = settings.dungeonFloor.gameObject.AddComponent<StageSceneryRenderer>();
            Bounds field = settings.dungeonFloor.TryGetWorldBounds(out Bounds floorBounds) ? floorBounds :
                new Bounds(Vector3.zero, settings.fieldSize);
            scenery.Configure(Current.scenery, field);
            if (settings.gameplayCamera != null) settings.gameplayCamera.backgroundColor = Current.background;
        }
    }
}
