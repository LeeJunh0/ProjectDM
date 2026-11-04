using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Pooled approved skill artwork; all timing/motion is presentation-only, with legacy fallbacks.</summary>
    internal sealed class PrototypeSkillVisuals : MonoBehaviour
    {
        private readonly Queue<SpriteRenderer> spritePool = new();
        private readonly List<Visual> active = new();
        private readonly Queue<TextMesh> textPool = new();
        private readonly List<FloatingText> texts = new();
        private readonly List<SpriteRenderer> frostShards = new();
        private Sprite bolt, gem;
        private SkillVisualAssets artwork;
        private Material defaultSpriteMaterial;
        private BossDeathRippleVisuals deathRipples;
        private Sprite lineSprite;
        private SpriteQuadBatch batch;
        private struct BatchEffect {internal Sprite sprite;internal string clip;internal Vector3 center;internal Vector2 size;internal float angle,age,duration;internal Color color;}
        private readonly List<BatchEffect> batched = new();
        internal int BatchedEffectCount => batched.Count;
        void AddBatch(Sprite sprite,Vector3 center,Vector2 size,float angle,Color color,float duration)
        {if(sprite==null)return;batched.Add(new BatchEffect{sprite=sprite,center=center,size=size,angle=angle,color=color,duration=Mathf.Max(.01f,duration)});}
        internal bool PixelEffect(string clip,Vector3 center,Vector2 size,float angle,float duration,Color color)
        {
            Sprite frame=PixelFxLibrary.Frame(clip,0);if(frame==null)return false;
            batched.Add(new BatchEffect{sprite=frame,clip=clip,center=center,size=size,angle=angle,color=color,duration=Mathf.Max(.01f,duration)});return true;
        }
        void TickBatch(float dt)
        {
            batch ??= new SpriteQuadBatch("전체 스킬 이펙트 일괄 그리기",6);batch.Begin();
            for(int i=batched.Count-1;i>=0;i--){var e=batched[i];e.age+=dt;
                if(e.age>=e.duration){batched[i]=batched[batched.Count-1];batched.RemoveAt(batched.Count-1);continue;}
                if(e.clip!=null)e.sprite=PixelFxLibrary.Frame(e.clip,e.age);
                Color color=e.color;color.a*=1-e.age/e.duration;
                batch.Add(e.sprite,e.center,e.size,e.angle,color);batched[i]=e;}
            batch.End();
        }

        internal void BossDeath(Vector3 position, bool boss)
        {
            if (deathRipples == null) deathRipples = gameObject.GetComponent<BossDeathRippleVisuals>() ?? gameObject.AddComponent<BossDeathRippleVisuals>();
            deathRipples.Emit(position, boss);
        }

        private sealed class Visual
        {
            public SpriteRenderer renderer;
            public Color color;
            public Vector3 from, to, size;
            public float age, duration, expansionDuration, startScale = .2f;
            public bool falling, expanding, lightning, warning;
            public string clip;
        }

        private sealed class FloatingText
        {
            public TextMesh mesh;
            public Vector3 origin;
            public float age;
        }

        internal void Configure(Sprite boltSprite, Sprite gemSprite, SkillVisualAssets assets = null)
        {
            bolt = boltSprite; gem = gemSprite; artwork = assets;
        }

        private SpriteRenderer Rent(Sprite sprite, bool warmTint = false)
        {
            if(sprite == null) return null;

            SpriteRenderer renderer;
            if (spritePool.Count > 0) renderer = spritePool.Dequeue();
            else
            {
                GameObject instance = new GameObject("스킬 이펙트");
                instance.transform.SetParent(transform, false);
                // Separate connection-axis scale from the diagonal artwork's local rotation.
                // This lets lightning widen without moving either authored endpoint.
                GameObject spriteObject = new GameObject("스프라이트");
                spriteObject.transform.SetParent(instance.transform, false);
                renderer = spriteObject.AddComponent<SpriteRenderer>();
                defaultSpriteMaterial ??= renderer.sharedMaterial;
            }
            renderer.sprite = sprite;
            renderer.sharedMaterial = warmTint && artwork != null && artwork.impactMaterial != null
                ? artwork.impactMaterial : defaultSpriteMaterial;
            renderer.sortingOrder = 6;
            Transform frame = renderer.transform.parent;
            frame.localPosition = Vector3.zero;
            frame.localRotation = Quaternion.identity;
            frame.localScale = Vector3.one;
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.rotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one;
            renderer.color = Color.white;
            renderer.flipX = renderer.flipY = false;
            renderer.gameObject.SetActive(true);
            return renderer;
        }

        private static Vector3 SpriteScale(Sprite sprite, float width, float height)
        {
            Vector2 size = sprite != null ? sprite.bounds.size : Vector2.one;
            return new Vector3(width / Mathf.Max(.01f, size.x), height / Mathf.Max(.01f, size.y), 1);
        }
        internal void SkillLine(Vector3 from, Vector3 to, float width, Color color, float duration)
        {
            Vector3 axis=to-from;
            if(axis.sqrMagnitude>.00001f && PixelEffect(color.r>.65f && color.b>.7f?"beam":"chain",(from+to)*.5f,
                new Vector2(axis.magnitude*1.2f,width*4),Mathf.Atan2(axis.y,axis.x)*Mathf.Rad2Deg,duration,new Color(1,1,1,color.a)))return;
            lineSprite ??= Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),new Vector2(.5f,.5f),Texture2D.whiteTexture.width);
            Vector3 delta=to-from;if(delta.sqrMagnitude<.00001f)return;
            AddBatch(lineSprite,(from+to)*.5f,new Vector2(delta.magnitude,width),Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,color,duration);
        }
        internal void SkillPulse(Vector3 center,float radius,string icon,Color color,float duration)
        {
            string clip=icon switch{"skill.projectile.satellite"=>"satellite","skill.flame.firefield"=>"firefield","skill.lightning.orb"=>"orb","skill.frost.blizzard"=>"blizzard","skill.frost.shatter"=>"shatter",_=>null};
            float span=clip=="satellite"||clip=="orb"?30:64;
            if(clip!=null&&PixelEffect(clip,center,Vector2.one*(radius*2*96/span),0,duration,new Color(1,1,1,color.a)))return;
            Sprite sprite=SkillIconAtlas.Find(icon) ?? gem ?? bolt;
            AddBatch(sprite,center,Vector2.one*radius*2,0,new Color(1,1,1,color.a),duration);
        }
        internal void BeginFrostOrbit(int count, float radius)
        {
            EndFrostOrbit();
            bool generated = artwork != null && artwork.frostShard != null;
            float diameter = generated ? Mathf.Clamp(artwork.frostArtworkDiameter, .1f, 1) : 1;
            for (int i = 0; i < count; i++)
            {
                Sprite pixelShard=PixelFxLibrary.Frame("frostshard",0);
                SpriteRenderer renderer = Rent(pixelShard != null ? pixelShard : generated ? artwork.frostShard : gem ?? bolt);
                if (renderer == null) break; // Visual budget never changes combat's shard count.
                renderer.transform.localScale = SpriteScale(renderer.sprite, radius * 2 / (pixelShard!=null?24f/96:diameter), radius * 2 / (pixelShard!=null?24f/96:diameter));
                renderer.color = pixelShard!=null||generated ? Color.white : new Color(.65f, .95f, 1, .95f);
                frostShards.Add(renderer);
            }
        }
        internal void MoveFrostShard(int index, Vector3 position, float angle)
        {
            if (index >= frostShards.Count) return;
            if(frostShards[index].sprite.name.StartsWith("PixelFX."))frostShards[index].sprite=PixelFxLibrary.Frame("frostshard",Time.time+index*.07f);
            frostShards[index].transform.position = position;
            frostShards[index].transform.rotation = Quaternion.Euler(0, 0, angle);
        }
        internal void EndFrostOrbit()
        {
            foreach (SpriteRenderer shard in frostShards) { shard.gameObject.SetActive(false); spritePool.Enqueue(shard); }
            frostShards.Clear();
        }

        internal void Link(Vector3 from, Vector3 to)
        {
            Vector3 direction=to-from;if(direction.sqrMagnitude<.0001f)return;
            if(PixelEffect("chain",(from+to)*.5f,new Vector2(direction.magnitude*1.2f,.9f),Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg,.18f,Color.white))return;
            if(artwork!=null && artwork.chain!=null)
            {
                Sprite sprite=artwork.chain;Vector3 start=Anchor(sprite,artwork.chainStart),end=Anchor(sprite,artwork.chainEnd),axis=end-start;
                if(axis.sqrMagnitude>.0001f)
                {
                    float scale=direction.magnitude/axis.magnitude,angle=(Mathf.Atan2(direction.y,direction.x)-Mathf.Atan2(axis.y,axis.x))*Mathf.Rad2Deg;
                    Vector3 center=from-Quaternion.Euler(0,0,angle)*start*scale;
                    AddBatch(sprite,center,(Vector2)sprite.bounds.size*scale,angle,new Color(1,1,1,Mathf.Clamp01(artwork.chainOpacity)),artwork.chainDuration);return;
                }
            }
            Vector3 perpendicular=new Vector3(-direction.y,direction.x).normalized*.14f;
            Vector3 a=from+direction/3+perpendicular,b=from+direction*2/3-perpendicular;
            SkillLine(from,a,.1f,new Color(.5f,.75f,1),.18f);SkillLine(a,b,.1f,new Color(.5f,.75f,1),.18f);SkillLine(b,to,.1f,new Color(.5f,.75f,1),.18f);
        }

        internal void Thunder(Vector3 from,Vector3 to)
        {
            float length=Vector3.Distance(from,to);
            if(!PixelEffect("thunder",(from+to)*.5f,new Vector2(1.1f,length*96/72),0,.25f,Color.white))Link(from,to);
        }

        internal void SatelliteShot(Vector3 point,Vector3 direction)
        {
            if(!PixelEffect("missile",point,Vector2.one*1.8f,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg,.09f,Color.white))
                SkillLine(point-direction*.15f,point,.12f,new Color(.8f,.4f,1),.12f);
        }

        internal void HitSpark(Vector3 point,CombatDamageElement element)
        {
            Color tint=element switch{CombatDamageElement.Flame=>new Color(1,.65f,.3f),CombatDamageElement.Frost=>new Color(.6f,.9f,1),
                CombatDamageElement.Lightning=>new Color(.7f,.8f,1),_=>new Color(.9f,.65f,1)};
            PixelEffect("hit",point,Vector2.one*2,0,.25f,tint);
        }

        private void Stroke(Vector3 from, Vector3 to)
        {
            SpriteRenderer renderer = Rent(bolt);
            if (renderer == null) return;
            Vector3 direction = to - from;
            renderer.transform.position = (from + to) * .5f;
            renderer.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            Vector3 size = SpriteScale(bolt, Mathf.Max(.05f, direction.magnitude), .1f);
            renderer.transform.localScale = size;
            Color color = new Color(.7f, .85f, 1f);
            renderer.color = color;
            active.Add(new Visual { renderer = renderer, color = color, size = size, duration = .18f });
        }

        // Placeholder fan made from the existing projectile sprite. Its rays use the actual hit range/angle.
        internal void FlameCone(Vector3 origin, Vector2 direction, float range, float fullAngle)
        {
            float heading = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float height=2*range*Mathf.Sin(Mathf.Clamp(fullAngle,1,180)*.5f*Mathf.Deg2Rad);
            if(PixelEffect("cone",origin+(Vector3)direction*(range*36/42),new Vector2(range*96/42,height*64/40),heading,.24f,Color.white))return;
            for (int i = 0; i <= 8; i++)
            {
                SpriteRenderer renderer = Rent(bolt, true);
                if (renderer == null) break;
                float angle = heading - fullAngle * .5f + fullAngle * i / 8f;
                Vector3 ray = Quaternion.Euler(0, 0, angle) * Vector3.right;
                renderer.transform.position = origin + ray * range * .5f;
                renderer.transform.rotation = Quaternion.Euler(0, 0, angle);
                Vector3 size = SpriteScale(bolt, range, .16f);
                renderer.transform.localScale = size;
                Color color = new Color(1, .35f, .06f, .5f);
                renderer.color = color;
                active.Add(new Visual { renderer = renderer, color = color, size = size, duration = .22f });
            }
        }

        internal void Burst(Vector3 center, float radius, Color color, float duration)
        {
            if(PixelEffect("hit",center,Vector2.one*radius*5,0,duration,Color.white))return;
            bool generated = artwork != null && artwork.splash != null;
            Expand(center, radius, generated ? artwork.splash : gem,
                generated ? new Color(1, 1, 1, Mathf.Clamp01(artwork.splashOpacity)) : new Color(color.r, color.g, color.b, .35f),
                generated ? artwork.splashDuration : duration, false);
        }

        internal void MeteorImpact(Vector3 center, float radius)
        {
            if(PixelEffect("fireimpact",center,Vector2.one*radius*3.2f,0,.4f,Color.white))return;
            bool generated = artwork != null && artwork.splash != null && artwork.impactMaterial != null;
            Color color = generated ? artwork.impactColor : new Color(1, .45f, .12f);
            color.a = generated ? Mathf.Clamp01(artwork.impactOpacity) : .35f;
            Expand(center, radius, generated ? artwork.splash : gem, color,
                generated ? artwork.impactDuration : .4f, generated);
        }

        private void Expand(Vector3 center, float radius, Sprite sprite, Color color, float duration, bool warmTint)
        {
            SpriteRenderer renderer = Rent(sprite, warmTint);
            if (renderer == null) return;
            renderer.transform.position = center;
            float diameter = artwork != null && sprite == artwork.splash ? Mathf.Clamp(artwork.burstArtworkDiameter, .1f, 1) : 1;
            Vector3 size = SpriteScale(sprite, radius * 2 / diameter, radius * 2 / diameter);
            float start = artwork != null ? Mathf.Clamp(artwork.burstStartScale, .01f, 1) : .2f;
            renderer.transform.localScale = size * start;
            renderer.color = color;
            float lifetime = Mathf.Max(.01f, duration);
            active.Add(new Visual { renderer = renderer, color = color, size = size,
                duration = lifetime, expansionDuration = Mathf.Clamp(artwork != null ? artwork.burstExpansionDuration : .06f,
                    .001f, lifetime * .5f), expanding = true, startScale = start });
        }

        internal void Meteor(Vector3 center, float radius, float delay)
        {
            // Motion uses the existing damage deadline, not a second combat timer.
            bool generated = artwork != null && artwork.meteor != null;
            bool warmMarker = generated && artwork.splash != null && artwork.impactMaterial != null;
            Sprite markerSprite = warmMarker ? artwork.splash : gem;
            SpriteRenderer marker = Rent(markerSprite, warmMarker);
            if (marker != null)
            {
                marker.transform.position = center;
                float diameter = warmMarker ? Mathf.Clamp(artwork.burstArtworkDiameter, .1f, 1) : 1;
                Vector3 markerSize = SpriteScale(markerSprite, radius * 2 / diameter, radius * 2 / diameter);
                marker.transform.localScale = markerSize;
                marker.sortingOrder = 1;
                Color warning = generated ? artwork.impactColor : new Color(1, .4f, .1f);
                warning.a = generated ? Mathf.Clamp01(artwork.warningOpacity) : .22f;
                marker.color = warning;
                active.Add(new Visual { renderer = marker, color = warning, size = markerSize,
                    duration = Mathf.Max(.01f, delay), warning = true });
            }
            Sprite sprite = generated ? artwork.meteor : bolt;
            Sprite pixelMeteor=PixelFxLibrary.Frame("meteor",0);
            if(pixelMeteor!=null)sprite=pixelMeteor;
            SpriteRenderer falling = Rent(sprite);
            if (falling == null) return;
            falling.transform.rotation = Quaternion.Euler(0, 0, pixelMeteor!=null?0:generated ? artwork.meteorRotation : -90);
            float width = generated ? Mathf.Max(.01f, artwork.meteorWidth) : .9f;
            if(pixelMeteor!=null)width*=96f/18;
            float height = generated ? width * sprite.rect.height / sprite.rect.width : .55f;
            Vector3 size = SpriteScale(sprite, width, height);
            falling.transform.localScale = size;
            Vector3 offset = generated && pixelMeteor==null ? falling.transform.rotation * Vector3.Scale(Anchor(sprite, artwork.meteorAnchor), size) : Vector3.zero;
            Vector3 end = center - offset;
            falling.transform.position = end + Vector3.up * (generated ? Mathf.Max(0, artwork.meteorFallHeight) : 3);
            falling.color = pixelMeteor!=null||generated ? Color.white : new Color(1f, .55f, .2f);
            active.Add(new Visual { renderer = falling, color = falling.color, size = size,
                from = falling.transform.position, to = end, duration = Mathf.Max(.01f, delay), falling = true,clip=pixelMeteor!=null?"meteor":null });
        }

        // Normalized anchors remain correct after import resizing and with non-central sprite pivots.
        internal static Vector3 Anchor(Sprite sprite, Vector2 normalized) => new(
            (normalized.x * sprite.rect.width - sprite.pivot.x) / sprite.pixelsPerUnit,
            (normalized.y * sprite.rect.height - sprite.pivot.y) / sprite.pixelsPerUnit, 0);

        internal void ManaAbsorb(Vector3 position, float amount)
        {
            TextMesh text;
            if (textPool.Count > 0) text = textPool.Dequeue();
            else
            {
                GameObject instance = new GameObject("마나 흡수 숫자");
                instance.transform.SetParent(transform, false);
                text = instance.AddComponent<TextMesh>();
                text.fontSize = 40;
                text.characterSize = .045f;
                text.anchor = TextAnchor.MiddleCenter;
                text.GetComponent<MeshRenderer>().sortingOrder = 10;
            }
            text.gameObject.SetActive(true);
            text.text = "+" + amount.ToString("0.##") + " MP";
            text.color = new Color(.4f, .8f, 1f);
            text.transform.position = position + Vector3.up * .45f;
            texts.Add(new FloatingText { mesh = text, origin = text.transform.position });
        }

        internal void Tick(float dt)
        {
            if (dt <= 0) return;
            deathRipples?.Tick(dt);
            TickBatch(dt);
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Visual visual = active[i];
                visual.age += dt;
                if(visual.clip!=null)visual.renderer.sprite=PixelFxLibrary.Frame(visual.clip,visual.age);
                float progress = Mathf.Clamp01(visual.age / visual.duration);
                if (visual.falling) visual.renderer.transform.position = Vector3.Lerp(visual.from, visual.to, progress * progress);
                if (visual.expanding)
                {
                    float expansion = Mathf.Clamp01(visual.age / visual.expansionDuration);
                    visual.renderer.transform.localScale = visual.size * Mathf.Lerp(visual.startScale, 1, 1 - Mathf.Pow(1 - expansion, 3));
                }
                Color color = visual.color;
                if (visual.warning) color.a *= .55f + .45f * progress;
                else if (visual.expanding) color.a *= 1 - Mathf.Clamp01((visual.age - visual.expansionDuration) /
                    Mathf.Max(.001f, visual.duration - visual.expansionDuration));
                else if (!visual.falling) color.a *= 1f - progress;
                if (visual.lightning) color.a *= .7f + .3f * Mathf.Abs(Mathf.Sin(visual.age * 90));
                visual.renderer.color = color;
                if (progress < 1f) continue;
                visual.renderer.gameObject.SetActive(false);
                spritePool.Enqueue(visual.renderer);
                active.RemoveAt(i);
            }
            for (int i = texts.Count - 1; i >= 0; i--)
            {
                FloatingText text = texts[i];
                text.age += dt;
                text.mesh.transform.position = text.origin + Vector3.up * text.age * .7f;
                text.mesh.color = new Color(.4f, .8f, 1f, Mathf.Clamp01(1f - text.age / .7f));
                if (text.age < .7f) continue;
                text.mesh.gameObject.SetActive(false);
                textPool.Enqueue(text.mesh);
                texts.RemoveAt(i);
            }
        }

        private void OnDestroy(){batch?.Dispose();if(lineSprite!=null)Destroy(lineSprite);}

        internal void Clear()
        {
            deathRipples?.Clear();
            batched.Clear();batch?.Clear();
            EndFrostOrbit();
            foreach (Visual visual in active) { visual.renderer.gameObject.SetActive(false); spritePool.Enqueue(visual.renderer); }
            active.Clear();
            foreach (FloatingText text in texts) { text.mesh.gameObject.SetActive(false); textPool.Enqueue(text.mesh); }
            texts.Clear();
        }
    }
}
