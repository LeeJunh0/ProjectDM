using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Two unlocked support skills charge from paid casts. Their hits never charge further skills.</summary>
    internal sealed class CharacterArsenalSystem
    {
        readonly CharacterSession session;
        readonly EnemySystem enemies;
        readonly PrototypeSkillVisuals visuals;
        readonly Func<string, float> amount;
        readonly Func<string, bool> unlocked;
        readonly Func<float> damage;
        readonly Action<Enemy, float> hit;
        readonly List<Enemy> targets = new();
        readonly List<Enemy> collisionCandidates = new();
        readonly HashSet<Enemy> visited = new();
        readonly HashSet<Enemy> scheduled = new();
        readonly List<Zone> zones = new();
        readonly List<Beam> beams = new();
        readonly List<Bolt> bolts = new();
        readonly List<Impact> impacts = new();
        readonly int[] charges = new int[2];
        readonly float[] cooldowns = new float[2];
        float clock, satelliteVisual;
        int satelliteVolley;
        Transform caster;
        sealed class Zone { internal string id; internal Vector3 center, velocity; internal float remaining, next, radius, power; }
        sealed class Beam { internal Vector3 direction; internal float remaining, next, width, power, sweep; internal readonly HashSet<Enemy> hits = new(); }
        sealed class Bolt { internal Vector3 center, direction; internal float remaining, power, speed; }
        sealed class Impact { internal Vector3 center; internal float remaining, radius, power; }
        internal int ActiveEffects => zones.Count + beams.Count + bolts.Count + impacts.Count;

        internal CharacterArsenalSystem(CharacterSession session, EnemySystem enemies, PrototypeSkillVisuals visuals,
            Func<float> damage, Func<string, float> amount, Func<string, bool> unlocked, Action<Enemy, float> hit)
        { this.session = session; this.enemies = enemies; this.visuals = visuals; this.damage = damage; this.amount = amount; this.unlocked = unlocked; this.hit = hit; }

        internal void PaidCast(Transform source)
        {
            caster = source;
            if (session?.Current?.skills == null) return;
            for (int i = 0; i < 2; i++)
                if (unlocked(session.Current.skills[i + 1].id)) charges[i] = Mathf.Min(session.Current.skills[i + 1].charge * 2, charges[i] + 1);
        }

        internal void Tick(float dt, Transform source)
        {
            if (dt <= 0 || session?.Current == null || enemies == null) return;
            if (source != null) caster = source;
            if (caster == null) return;
            clock += dt;
            for (int i = 0; i < 2; i++)
            {
                cooldowns[i] = Mathf.Max(0, cooldowns[i] - dt);
                var skill = session.Current.skills[i + 1];
                if (!unlocked(skill.id) || charges[i] < skill.charge || cooldowns[i] > 0) continue;
                Enemy target = Nearest(caster.position, skill.range);
                if (target == null) continue;
                charges[i] -= skill.charge;
                cooldowns[i] = skill.interval / (1 + (skill.id == "satellite" ? amount("satelliteRate") : 0));
                Cast(skill, target);
            }
            TickBeams(dt); TickZones(dt); TickBolts(dt); TickImpacts(dt);
            if (session.Current.id == "projectile" && unlocked("satellite") && clock >= satelliteVisual)
            {
                satelliteVisual = clock + .18f;
                int count = Mathf.Clamp(session.Current.skills[2].count + Mathf.FloorToInt(amount("satelliteCount")), 1, 8);
                for (int i = 0; i < count; i++)
                    visuals?.SkillPulse(Satellite(i, count), .22f, "skill.projectile.satellite", new Color(.8f,.35f,1), .22f);
            }
        }

        float Power(CharacterSkillDefinition s) => damage() * s.damage * (1 + amount(s.id + "Power"));
        Vector3 Satellite(int i, int count) => caster.position + Quaternion.Euler(0,0,clock * 35 + i * 360f / count) * Vector3.right * 1.1f;
        void Cast(CharacterSkillDefinition s, Enemy target)
        {
            float power = Power(s), radius = s.radius + amount(s.id + "Range");
            int count = Mathf.Clamp(s.count + Mathf.FloorToInt(amount(s.id + "Count")), 1, 8);
            Vector3 direction = (target.transform.position - caster.position).normalized;
            if (direction.sqrMagnitude < .001f) direction = Vector3.right;
            switch (s.id)
            {
                case "beam":
                    for (int i = 0; i < count; i++) beams.Add(new Beam { direction = Quaternion.Euler(0,0,(i-(count-1)*.5f)*18) * direction,
                        remaining=s.duration, width=s.radius+amount("beamWidth"), power=power, sweep=amount("beamSweep") });
                    break;
                case "satellite":
                    satelliteVolley++;
                    bool volley = amount("satelliteVolley") > 0 && satelliteVolley % Mathf.Max(2, 5 - Mathf.FloorToInt(amount("satelliteVolley"))) == 0;
                    visited.Clear();
                    for (int i=0;i<count;i++)
                    {
                        Vector3 from=Satellite(i,count);
                        Enemy next=Nearest(from,s.range,visited) ?? target; visited.Add(next);
                        Shoot(from,next.transform.position,power,s.speed);
                        if(volley)Shoot(from,next.transform.position,power,s.speed);
                    }
                    break;
                case "meteor":
                    Vector3 center=DenseCenter(target,s.range,radius); visited.Clear();
                    for(int i=0;i<count;i++)
                    {
                        Enemy next=i==0?target:Nearest(center,s.range,visited);
                        Vector3 point=i==0?center:next!=null?next.transform.position:center;
                        if(next!=null)visited.Add(next);
                        impacts.Add(new Impact{center=point,remaining=s.duration+i*.15f,radius=radius,power=power});
                        visuals?.Meteor(point,radius,s.duration+i*.15f);
                    }
                    break;
                case "thunder":
                    visited.Clear();
                    for(int i=0;i<count;i++)
                    {
                        Enemy next=Nearest(target.transform.position,s.range,visited); if(next==null)break;
                        visited.Add(next); Vector3 point=next.transform.position;
                        visuals?.Link(point+Vector3.up*5,point); Area(point,radius,power);
                        for(int j=0;j<Mathf.Min(3,Mathf.FloorToInt(amount("thunderChain")));j++)
                        { Enemy tail=Nearest(point,3,visited);if(tail==null)break;visited.Add(tail);visuals?.Link(point,tail.transform.position);point=tail.transform.position;hit(tail,power*.5f); }
                    }
                    break;
                case "shatter": Shatter(s,power,radius); break;
                default:
                    for(int i=0;i<count;i++)
                    {
                        Vector3 point=DenseCenter(target,s.range,radius)+ (i==0?Vector3.zero:Quaternion.Euler(0,0,i*137.5f)*Vector3.right*.8f);
                        zones.Add(new Zone{id=s.id,center=s.id=="orb"?caster.position:point,velocity=Quaternion.Euler(0,0,(i-(count-1)*.5f)*25)*direction*s.speed,
                            remaining=s.duration+amount(s.id+"Duration"),radius=radius,power=power,next=s.tickInterval});
                    }
                    break;
            }
        }

        void TickBeams(float dt)
        {
            for(int i=beams.Count-1;i>=0;i--)
            {
                Beam b=beams[i]; float step=Mathf.Min(dt,b.remaining);b.remaining-=step;
                // Sample the sweep so a low frame rate cannot skip narrow targets.
                int samples=Mathf.Clamp(Mathf.CeilToInt(b.sweep*step/5),1,24);
                for(int sample=0;sample<samples;sample++)
                {
                    b.direction=Quaternion.Euler(0,0,b.sweep*step/samples)*b.direction;
                    Vector3 end=caster.position+b.direction*session.Current.skills[1].range;
                    targets.Clear();foreach(var e in enemies.enemies)if(EnemySystem.IsLive(e)&&!b.hits.Contains(e)&&SegmentDistance(e.transform.position,caster.position,end)<=b.width+.35f*e.hitRadiusMultiplier)targets.Add(e);
                    foreach(var e in targets){b.hits.Add(e);hit(e,b.power);}
                }
                if(clock>=b.next){b.next=clock+.08f;visuals?.SkillLine(caster.position,caster.position+b.direction*session.Current.skills[1].range,b.width*2,new Color(.85f,.4f,1),.12f);}
                if(b.remaining<=0)beams.RemoveAt(i);
            }
        }

        void TickZones(float dt)
        {
            for(int i=zones.Count-1;i>=0;i--)
            {
                Zone z=zones[i];float step=Mathf.Min(dt,z.remaining);z.remaining-=step;
                if(z.id=="orb")z.center+=z.velocity*step;
                if(z.id=="blizzard") {Enemy follow=Nearest(z.center,6);if(follow!=null)z.center=Vector3.MoveTowards(z.center,follow.transform.position,session.Current.skills[1].speed*step);}
                var s=Array.Find(session.Current.skills,skill=>skill.id==z.id);
                // At most duration/tickInterval pulses, including the final fraction; frame-rate independent deadlines.
                z.next-=step;
                while(z.next<=.00001f)
                {
                    z.next+=s.tickInterval;
                    Color tint=z.id=="firefield"?new Color(1,.3f,.05f,.6f):z.id=="orb"?new Color(.5f,.75f,1,.7f):new Color(.4f,.9f,1,.6f);
                    visuals?.SkillPulse(z.center,z.radius,"skill."+session.Current.id+"."+z.id,tint,s.tickInterval);
                    targets.Clear();foreach(var e in enemies.enemies)if(EnemySystem.IsLive(e)&&(e.transform.position-z.center).sqrMagnitude<=z.radius*z.radius)targets.Add(e);
                    foreach(var e in targets)
                    {
                        if(z.id=="orb")visuals?.Link(z.center,e.transform.position);
                        hit(e,z.power);
                        if(z.id=="blizzard"&&EnemySystem.IsLive(e))Cold(e,1+Mathf.FloorToInt(amount("blizzardCold")));
                    }
                    if(z.id=="orb"&&amount("orbLink")>0)
                        foreach(var other in zones)if(other!=z&&other.id=="orb"&&(other.center-z.center).sqrMagnitude<49)
                        {
                            visuals?.SkillLine(z.center,other.center,.1f,tint,.2f);
                            targets.Clear();foreach(var e in enemies.enemies)if(EnemySystem.IsLive(e)&&SegmentDistance(e.transform.position,z.center,other.center)<.3f)targets.Add(e);
                            foreach(var e in targets)hit(e,z.power*amount("orbLink"));
                        }
                }
                if(z.remaining<=0)zones.RemoveAt(i);
            }
        }
        internal void Cold(Enemy enemy,int stacks)
        {
            var c=session.Current;enemy.chillRemaining=c.chillDuration;enemy.chillSlow=c.slowFraction;
            if(enemy.freezeRemaining>0)return;
            enemy.chillStacks+=stacks;if(enemy.chillStacks<c.chillThreshold)return;
            enemy.chillStacks=0;enemy.freezeRemaining=c.freezeSeconds+amount("freezeDuration");
        }
        void Shatter(CharacterSkillDefinition s,float power,float radius)
        {
            visuals?.SkillPulse(caster.position,radius,s.icon,new Color(.5f,.95f,1),.45f);
            targets.Clear();foreach(var e in enemies.enemies)if(EnemySystem.IsLive(e)&&(e.transform.position-caster.position).sqrMagnitude<=radius*radius)targets.Add(e);
            // A bounded breadth-first cascade. A frozen monster can explode once per wave, without recursive calls.
            var wave=new Queue<(Enemy enemy,int depth)>();visited.Clear();scheduled.Clear();
            foreach(var e in targets){if(e.freezeRemaining>0){wave.Enqueue((e,0));scheduled.Add(e);}else hit(e,power*.35f);}
            int maxDepth=Mathf.Min(3,Mathf.FloorToInt(amount("shatterCascade"))), shards=Mathf.Min(6,Mathf.FloorToInt(amount("shatterShards")));
            while(wave.Count>0)
            {
                var item=wave.Dequeue();Enemy e=item.enemy;if(!EnemySystem.IsLive(e)||!visited.Add(e))continue;
                Vector3 point=e.transform.position;hit(e,power*2);visuals?.SkillPulse(point,.7f,s.icon,new Color(.65f,1,1),.25f);
                for(int j=0;j<shards;j++)
                {
                    Enemy next=Nearest(point,2.5f,scheduled);if(next==null)break;scheduled.Add(next);
                    bool frozen=next.freezeRemaining>0;visuals?.SkillLine(point,next.transform.position,.12f,new Color(.5f,.9f,1),.18f);
                    if(frozen&&item.depth<maxDepth)wave.Enqueue((next,item.depth+1));else hit(next,power*.5f);
                }
            }
        }
        void TickImpacts(float dt)
        {
            for(int i=impacts.Count-1;i>=0;i--){var p=impacts[i];p.remaining-=dt;if(p.remaining>0)continue;
                targets.Clear();foreach(var e in enemies.enemies)if(EnemySystem.IsLive(e)&&(e.transform.position-p.center).sqrMagnitude<=p.radius*p.radius)targets.Add(e);
                foreach(var e in targets)hit(e,p.power*(1+Mathf.Min(5,e.burnStacks)*amount("meteorIgnite")));
                visuals?.MeteorImpact(p.center,p.radius);impacts.RemoveAt(i);}
        }
        void Shoot(Vector3 from,Vector3 to,float power,float speed)
        {bolts.Add(new Bolt{center=from,direction=(to-from).sqrMagnitude<.001f?Vector3.right:(to-from).normalized,power=power,speed=speed,remaining=2});}
        internal void Split(Vector3 point,float power,Enemy original)
        {if(amount("split")<1)return;visited.Clear();visited.Add(original);for(int i=0;i<Mathf.FloorToInt(amount("split"));i++){var e=Nearest(point,5,visited);if(e==null)break;visited.Add(e);Shoot(point,e.transform.position,power*.5f,10);}}
        void TickBolts(float dt)
        {
            for(int i=bolts.Count-1;i>=0;i--){var b=bolts[i];Vector3 end=b.center+b.direction*b.speed*dt;Enemy nearest=null;float along=float.MaxValue;
                enemies.spatial.Bounds(Vector3.Min(b.center,end)-Vector3.one*1.5f,Vector3.Max(b.center,end)+Vector3.one*1.5f,collisionCandidates);
                foreach(var e in collisionCandidates)if(EnemySystem.IsLive(e)&&SegmentDistance(e.transform.position,b.center,end)<.4f*e.hitRadiusMultiplier){float d=(e.transform.position-b.center).sqrMagnitude;if(d<along){nearest=e;along=d;}}
                visuals?.SkillLine(b.center,end,.12f,new Color(.8f,.4f,1),.12f);b.center=end;b.remaining-=dt;
                if(nearest!=null)hit(nearest,b.power);if(nearest!=null||b.remaining<=0)bolts.RemoveAt(i);}
        }
        void Area(Vector3 point,float radius,float power)
        {targets.Clear();foreach(var e in enemies.enemies)if(EnemySystem.IsLive(e)&&(e.transform.position-point).sqrMagnitude<=radius*radius)targets.Add(e);foreach(var e in targets)hit(e,power);}
        Enemy Nearest(Vector3 point,float radius,HashSet<Enemy> exclude=null)
        {Enemy result=null;float d=radius*radius;foreach(var e in enemies.enemies)if(EnemySystem.IsLive(e)&&(exclude==null||!exclude.Contains(e))){float next=(e.transform.position-point).sqrMagnitude;if(next<=d){d=next;result=e;}}return result;}
        Vector3 DenseCenter(Enemy fallback,float range,float radius)
        {
            // Fixed candidate budget avoids an all-pairs density scan in crowded fields.
            Vector3 best=fallback.transform.position;int bestCount=-1,candidates=0;
            foreach(var candidate in enemies.enemies){if(!EnemySystem.IsLive(candidate)||(candidate.transform.position-caster.position).sqrMagnitude>range*range)continue;
                int nearby=0;foreach(var e in enemies.enemies)if(EnemySystem.IsLive(e)&&(e.transform.position-candidate.transform.position).sqrMagnitude<=radius*radius)nearby++;
                if(nearby>bestCount){bestCount=nearby;best=candidate.transform.position;}if(++candidates>=12)break;}
            return best;
        }
        internal static float SegmentDistance(Vector3 point,Vector3 from,Vector3 to)
        {Vector3 line=to-from;float t=line.sqrMagnitude>.00001f?Mathf.Clamp01(Vector3.Dot(point-from,line)/line.sqrMagnitude):0;return (point-from-line*t).magnitude;}
        internal void Reset(){zones.Clear();beams.Clear();bolts.Clear();impacts.Clear();targets.Clear();visited.Clear();Array.Clear(charges,0,2);Array.Clear(cooldowns,0,2);clock=satelliteVisual=0;satelliteVolley=0;caster=null;}
    }
}
