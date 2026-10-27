using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    [CreateAssetMenu(menuName = "Project DM/스테이지 장식 배치", fileName = "StageScenery")]
    public sealed class StageSceneryLayout : ScriptableObject
    {
        [InspectorName("배치 기준 필드 크기")] public Vector2 referenceSize = new(100, 100);
        [InspectorName("장식 팔레트")] public Prop[] palette = Array.Empty<Prop>();
        [InspectorName("저장된 배치")] public List<Placement> placements = new();

        [Serializable]
        public sealed class Prop
        {
            public string id;
            [InspectorName("이름")] public string displayName;
            [InspectorName("스프라이트")] public Sprite sprite;
            [InspectorName("높이 (월드 단위)"), Min(.05f)] public float height = 1;
            [InspectorName("색상")] public Color tint = Color.white;
            [InspectorName("횃불 효과")] public bool flame;
        }

        [Serializable]
        public sealed class Placement
        {
            public string propId;
            [InspectorName("기준 필드 내 위치")] public Vector2 position;
            [InspectorName("크기"), Min(.1f)] public float scale = 1;
            [InspectorName("회전")] public float rotation;
            [InspectorName("좌우 반전")] public bool flipX;
        }

        public Prop Find(string id)
        {
            if (palette == null) return null;
            foreach (Prop prop in palette) if (prop != null && prop.id == id) return prop;
            return null;
        }

        public Vector2 ToWorld(Vector2 position, Bounds field) => (Vector2)field.center + Vector2.Scale(position,
            new Vector2(field.size.x / Mathf.Max(1, referenceSize.x), field.size.y / Mathf.Max(1, referenceSize.y)));

        public Vector2 FromWorld(Vector2 position, Bounds field) => Vector2.Scale(position - (Vector2)field.center,
            new Vector2(referenceSize.x / Mathf.Max(1, field.size.x), referenceSize.y / Mathf.Max(1, field.size.y)));

        public void ValidateContent()
        {
            if (palette == null || placements == null || referenceSize.x <= 0 || referenceSize.y <= 0 || !float.IsFinite(referenceSize.x) || !float.IsFinite(referenceSize.y))
                throw new InvalidOperationException(name + ": 장식 기준 필드 크기를 확인하세요.");
            var ids = new HashSet<string>();
            foreach (Prop prop in palette)
                if (prop == null || string.IsNullOrWhiteSpace(prop.id) || !ids.Add(prop.id) || prop.sprite == null ||
                    prop.height <= 0 || !float.IsFinite(prop.height))
                    throw new InvalidOperationException(name + ": 장식 ID·스프라이트·높이를 확인하세요.");
            foreach (Placement item in placements)
                if (item == null || !ids.Contains(item.propId) || item.scale <= 0 || !float.IsFinite(item.scale) ||
                    !float.IsFinite(item.position.x) || !float.IsFinite(item.position.y) || !float.IsFinite(item.rotation))
                    throw new InvalidOperationException(name + ": 장식 배치에 누락되거나 잘못된 값이 있습니다.");
        }
    }
}
