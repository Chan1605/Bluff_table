#if UNITY_EDITOR
using System;
using LiarsBar.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using T = LiarsBar.Presentation.PresentationTimings;

namespace LiarsBar.Presentation.EditorTools
{
    /// <summary>
    /// Tools > LiarsBar > Build Presentation Animators
    /// 연출용 Animator Controller 6개를 파라미터 · 상태 · 전이 · 상태 신호까지 한 번에 만든다.
    ///
    /// - 카드 · 이름표 · 카메라 · 테이블 표식 · 리볼버: 실제로 움직이는 시작용 클립까지 생성.
    /// - 캐릭터: 길이만 맞춘 빈 자리표시 클립 생성 → 캐릭터 에셋 애니메이션으로 교체.
    /// - 이미 있는 클립은 덮어쓰지 않는다 (Animation 창에서 다듬은 내용 보존).
    /// - 컨트롤러가 이미 있으면 덮어쓸지 묻는다.
    /// - 모든 상태 Write Defaults = Off: 상태가 바뀌어도 마지막 값(위치 · 켜짐)이 유지된다.
    /// </summary>
    public static class PresentationAnimatorBuilder
    {
        const string Root = "Assets/LiarsBar/Presentation/Animators";
        const float Blend = 0.12f;

        [MenuItem("Tools/LiarsBar/Build Presentation Animators")]
        public static void BuildAll()
        {
            EnsureFolder(Root);
            BuildCard();
            BuildNameTag();
            BuildCamera();
            BuildTableMarker();
            BuildRevolver();
            BuildCharacter();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[LiarsBar] 연출 Animator 생성 완료 → {Root}");
        }

        // ═════════════════════ 카드 ═════════════════════
        //  Hidden ─Deal→ Deal ─→ InHand ⇄ Lifted
        //                         └─Play→ Play ─→ OnTable ─Reveal→ Reveal ─┬ Match → RevealMatch
        //                                                                  └ !Match → RevealMiss
        //  Any ─Collect→ Collect ─→ Hidden       Any ─Hide→ Hidden
        static void BuildCard()
        {
            var c = NewController("Card");
            if (c == null) return;
            string dir = Folder("Card");
            Params(c, (AnimIds.Card.Deal, P.Trigger), (AnimIds.Card.Lifted, P.Bool), (AnimIds.Card.Play, P.Trigger),
                      (AnimIds.Card.Match, P.Bool), (AnimIds.Card.Reveal, P.Trigger), (AnimIds.Card.Collect, P.Trigger),
                      (AnimIds.Card.Hide, P.Trigger));

            const string vis = "Visual", flip = "Visual/Flip";
            const string gm = "Visual/Flip/GlowMatch", gx = "Visual/Flip/GlowMiss";

            var hidden = Clip(dir, "Card_Hidden", true, k =>
            {
                Active(k, vis, false); Const(k, "", typeof(CardView), "travel", 0f); Const(k, "", typeof(CardView), "arc", 0f);
                Active(k, gm, false); Active(k, gx, false); Euler(k, flip, 0f, 0f);
            });
            var deal = Clip(dir, "Card_Deal", false, k =>
            {
                Active(k, vis, true); Active(k, gm, false); Active(k, gx, false);
                Float(k, "", typeof(CardView), "travel", EaseOut(T.CardDealFlight));
                Float(k, "", typeof(CardView), "arc", Bump(T.CardDealFlight, 0.12f));
                Pos(k, vis, 0f, 0f); Euler(k, flip, 0f, 0f);
            });
            var inHand = Clip(dir, "Card_InHand", true, k => { Const(k, "", typeof(CardView), "travel", 1f); Const(k, "", typeof(CardView), "arc", 0f); Pos(k, vis, 0f, 0f); });
            var lifted = Clip(dir, "Card_Lifted", true, k => { Const(k, "", typeof(CardView), "travel", 1f); Pos(k, vis, 0.035f, 0.035f); });
            var play = Clip(dir, "Card_Play", false, k =>
            {
                Float(k, "", typeof(CardView), "travel", EaseInOut(T.CardPlay));
                Float(k, "", typeof(CardView), "arc", Bump(T.CardPlay, 0.08f));
                Pos(k, vis, 0f, 0f);
            });
            var onTable = Clip(dir, "Card_OnTable", true, k => { Const(k, "", typeof(CardView), "travel", 1f); Const(k, "", typeof(CardView), "arc", 0f); });
            var reveal = Clip(dir, "Card_Reveal", false, k =>
            {
                Pos(k, vis, 0f, 0f, T.CardReveal, 0.06f);          // 살짝 들었다가
                Euler(k, flip, 0f, 180f, T.CardReveal);            // 뒤집고
            });
            var match = Clip(dir, "Card_RevealMatch", true, k => { Active(k, gm, true); Active(k, gx, false); Euler(k, flip, 180f, 180f); });
            var miss = Clip(dir, "Card_RevealMiss", true, k => { Active(k, gm, false); Active(k, gx, true); Euler(k, flip, 180f, 180f); });
            var collect = Clip(dir, "Card_Collect", false, k =>
            {
                Float(k, "", typeof(CardView), "travel", EaseIn(T.CardCollect));
                Float(k, "", typeof(CardView), "arc", Bump(T.CardCollect, 0.05f));
                Active(k, gm, false); Active(k, gx, false);
                Pos(k, vis, 0f, 0f);
            });

            var sm = c.layers[0].stateMachine;
            var sHidden = State(sm, "Hidden", hidden, 0, 0);
            var sDeal = State(sm, "Deal", deal, 1, 0);
            var sHand = State(sm, "InHand", inHand, 2, 0);
            var sLift = State(sm, "Lifted", lifted, 2, 1);
            var sPlay = State(sm, "Play", play, 3, 0);
            var sTable = State(sm, "OnTable", onTable, 4, 0);
            var sReveal = State(sm, "Reveal", reveal, 5, 0);
            var sMatch = State(sm, "RevealMatch", match, 6, -1);
            var sMiss = State(sm, "RevealMiss", miss, 6, 1);
            var sCollect = State(sm, "Collect", collect, 3, 2);
            sm.defaultState = sHidden;

            Go(sHidden, sDeal, 0f).If(AnimIds.Card.Deal);
            Exit(sDeal, sHand, 0f);
            Go(sHand, sLift, 0.08f).If(AnimIds.Card.Lifted);
            Go(sLift, sHand, 0.08f).IfNot(AnimIds.Card.Lifted);
            Go(sHand, sPlay, 0f).If(AnimIds.Card.Play);
            Go(sLift, sPlay, 0f).If(AnimIds.Card.Play);
            Exit(sPlay, sTable, 0f);
            Go(sTable, sReveal, 0f).If(AnimIds.Card.Reveal);
            Exit(sReveal, sMatch, 0f).If(AnimIds.Card.Match);
            Exit(sReveal, sMiss, 0f).IfNot(AnimIds.Card.Match);
            Any(sm, sCollect, 0f).If(AnimIds.Card.Collect);
            Exit(sCollect, sHidden, 0f);
            Any(sm, sHidden, 0f).If(AnimIds.Card.Hide);
        }

        // ═════════════════════ 이름표 ═════════════════════
        //  Base:  Normal ⇄ Turn (IsTurn)     Any ─Dead→ Dead ─!Dead→ Normal
        //  Pulse: Empty ─Shot→ Punch ─→ Empty
        static void BuildNameTag()
        {
            var c = NewController("NameTag");
            if (c == null) return;
            string dir = Folder("NameTag");
            Params(c, (AnimIds.NameTag.IsTurn, P.Bool), (AnimIds.NameTag.Dead, P.Bool), (AnimIds.NameTag.Shot, P.Trigger));

            var normal = Clip(dir, "NameTag_Normal", true, k => { Const(k, "", typeof(CanvasGroup), "m_Alpha", 1f); Active(k, "Highlight", false); });
            var turn = Clip(dir, "NameTag_Turn", true, k => { Const(k, "", typeof(CanvasGroup), "m_Alpha", 1f); Active(k, "Highlight", true); });
            var dead = Clip(dir, "NameTag_Dead", true, k => { Const(k, "", typeof(CanvasGroup), "m_Alpha", 0.35f); Active(k, "Highlight", false); });
            var punch = Clip(dir, "NameTag_Punch", false, k => Scale(k, "Body", Bump(0.25f, 0.25f, 1f)));

            var sm = c.layers[0].stateMachine;
            var sNormal = State(sm, "Normal", normal, 0, 0);
            var sTurn = State(sm, "Turn", turn, 1, 0);
            var sDead = State(sm, "Dead", dead, 1, 1);
            sm.defaultState = sNormal;
            Go(sNormal, sTurn, 0.15f).If(AnimIds.NameTag.IsTurn);
            Go(sTurn, sNormal, 0.15f).IfNot(AnimIds.NameTag.IsTurn);
            Any(sm, sDead, 0.3f).If(AnimIds.NameTag.Dead);
            Go(sDead, sNormal, 0f).IfNot(AnimIds.NameTag.Dead);

            c.AddLayer("Pulse");
            var layers = c.layers;
            layers[1].defaultWeight = 1f;
            c.layers = layers;
            var psm = c.layers[1].stateMachine;
            var sEmpty = State(psm, "Empty", null, 0, 0);
            var sPunch = State(psm, "Punch", punch, 1, 0);
            psm.defaultState = sEmpty;
            Go(sEmpty, sPunch, 0f).If(AnimIds.NameTag.Shot);
            Exit(sPunch, sEmpty, 0f);
        }

        // ═════════════════════ 카메라 ═════════════════════
        //  Free ⇄ Focus (Focused, 블렌드 0.45초 = 시선 이동 이징)
        //  Any ─Victory→ Victory (회전 루프)     Any ─Reset→ Free
        static void BuildCamera()
        {
            var c = NewController("Camera");
            if (c == null) return;
            string dir = Folder("Camera");
            Params(c, (AnimIds.Camera.Focused, P.Bool), (AnimIds.Camera.Victory, P.Trigger), (AnimIds.Camera.Reset, P.Trigger));
            var t = typeof(CameraDirector);

            var free = Clip(dir, "Camera_Free", true, k => { Const(k, "", t, "focusWeight", 0f); Const(k, "", t, "fovOffset", 0f); Const(k, "", t, "orbitDegrees", 0f); });
            var focus = Clip(dir, "Camera_Focus", true, k => { Const(k, "", t, "focusWeight", 1f); Const(k, "", t, "fovOffset", -12f); Const(k, "", t, "orbitDegrees", 0f); });
            var victory = Clip(dir, "Camera_Victory", true, k =>
            {
                Const(k, "", t, "focusWeight", 1f, 12f); Const(k, "", t, "fovOffset", -6f, 12f);
                Float(k, "", t, "orbitDegrees", AnimationCurve.Linear(0f, 0f, 12f, 360f));
            });

            var sm = c.layers[0].stateMachine;
            var sFree = State(sm, "Free", free, 0, 0);
            var sFocus = State(sm, "Focus", focus, 1, 0);
            var sVictory = State(sm, "Victory", victory, 1, 1);
            sm.defaultState = sFree;
            Go(sFree, sFocus, 0.45f).If(AnimIds.Camera.Focused);
            Go(sFocus, sFree, 0.45f).IfNot(AnimIds.Camera.Focused);
            Any(sm, sVictory, 1.0f).If(AnimIds.Camera.Victory);
            Any(sm, sFree, 0f).If(AnimIds.Camera.Reset);
        }

        // ═════════════════════ 테이블 카드 표식 ═════════════════════
        //  Hidden ─RoundStart & TableRank==0/1/2→ ShowK / ShowQ / ShowA      Any ─Clear→ Hidden
        static void BuildTableMarker()
        {
            var c = NewController("TableMarker");
            if (c == null) return;
            string dir = Folder("TableMarker");
            Params(c, (AnimIds.TableMarker.TableRank, P.Int), (AnimIds.TableMarker.RoundStart, P.Trigger), (AnimIds.TableMarker.Clear, P.Trigger));

            var hidden = Clip(dir, "Marker_Hidden", true, k => Active(k, "Marker", false));
            var sm = c.layers[0].stateMachine;
            var sHidden = State(sm, "Hidden", hidden, 0, 0);
            sm.defaultState = sHidden;
            Any(sm, sHidden, 0f).If(AnimIds.TableMarker.Clear);

            string[] ranks = { "K", "Q", "A" };
            for (int i = 0; i < ranks.Length; i++)
            {
                int rank = i;
                var show = Clip(dir, $"Marker_Show{ranks[i]}", false, k =>
                {
                    Active(k, "Marker", true);
                    for (int j = 0; j < ranks.Length; j++) Active(k, $"Marker/{ranks[j]}", j == rank);
                    Scale(k, "Marker", Pop(T.TableMarkerPop));
                });
                var s = State(sm, $"Show{ranks[i]}", show, 1, i - 1);
                var tr = Go(sHidden, s, 0f).If(AnimIds.TableMarker.RoundStart);
                tr.AddCondition(AnimatorConditionMode.Equals, i, AnimIds.TableMarker.TableRank);
            }
        }

        // ═════════════════════ 리볼버 ═════════════════════
        //  Idle ─Click→ Click ─→ Idle      Idle ─Fire→ Fire ─→ Idle      Any ─Reset→ Idle
        static void BuildRevolver()
        {
            var c = NewController("Revolver");
            if (c == null) return;
            string dir = Folder("Revolver");
            Params(c, (AnimIds.Revolver.Click, P.Trigger), (AnimIds.Revolver.Fire, P.Trigger), (AnimIds.Revolver.Reset, P.Trigger));

            var idle = Clip(dir, "Revolver_Idle", true, k => Active(k, "Muzzle", false));
            var click = Clip(dir, "Revolver_Click", false, k => Placeholder(k, 0.3f));
            var fire = Clip(dir, "Revolver_Fire", false, k => Stepped(k, "Muzzle", (0f, 1f), (0.08f, 0f), (0.5f, 0f)));

            var sm = c.layers[0].stateMachine;
            var sIdle = State(sm, "Idle", idle, 0, 0);
            var sClick = State(sm, "Click", click, 1, -1);
            var sFire = State(sm, "Fire", fire, 1, 1);
            sm.defaultState = sIdle;
            Go(sIdle, sClick, 0f).If(AnimIds.Revolver.Click);
            Exit(sClick, sIdle, 0f);
            Go(sIdle, sFire, 0f).If(AnimIds.Revolver.Fire);
            Exit(sFire, sIdle, 0f);
            Any(sm, sIdle, 0f).If(AnimIds.Revolver.Reset);
        }

        // ═════════════════════ 캐릭터 ═════════════════════
        //  Idle ⇄ TurnIdle (IsTurn)
        //  Any ─PlayCards / CallLiar / Accused / RevealWin→ (리액션) ─→ Idle
        //  Any ─RevealLose→ RevealLose ─→ Tense(루프, 룰렛 대기)
        //  Any ─Roulette→ PickUp → Aim → Hold ─┬ Survived  → Relief → PutDown → Idle
        //                                       └ !Survived → Death → DeadLoop
        //  Any ─ForceDead→ DeadLoop     Any ─Victory→ Victory     Any ─Reset→ Idle
        static void BuildCharacter()
        {
            var c = NewController("Character");
            if (c == null) return;
            string dir = Folder("Character");
            Params(c,
                (AnimIds.Character.IsTurn, P.Bool), (AnimIds.Character.PlayCards, P.Trigger), (AnimIds.Character.CallLiar, P.Trigger),
                (AnimIds.Character.Accused, P.Trigger), (AnimIds.Character.RevealWin, P.Trigger), (AnimIds.Character.RevealLose, P.Trigger),
                (AnimIds.Character.Survived, P.Bool), (AnimIds.Character.Roulette, P.Trigger), (AnimIds.Character.ForceDead, P.Trigger),
                (AnimIds.Character.Victory, P.Trigger), (AnimIds.Character.Reset, P.Trigger));

            AnimationClip Ph(string name, float len, bool loop) => Clip(dir, "Char_" + name, loop, k => Placeholder(k, len));

            var sm = c.layers[0].stateMachine;
            var idle = State(sm, "Idle", Ph("Idle", 2f, true), 0, 0);
            var turnIdle = State(sm, "TurnIdle", Ph("TurnIdle", 2f, true), 0, 1);
            var playCards = State(sm, "PlayCards", Ph("PlayCards", T.PlayCards, false), -1, -2);
            var callLiar = State(sm, "CallLiar", Ph("CallLiar", T.CallLiar, false), 0, -2);
            var accused = State(sm, "Accused", Ph("Accused", T.Accused, false), 1, -2);
            var revealWin = State(sm, "RevealWin", Ph("RevealWin", T.RevealReact, false), 2, -2);
            var revealLose = State(sm, "RevealLose", Ph("RevealLose", T.RevealReact, false), 2, -1);
            var tense = State(sm, "Tense", Ph("Tense", 2f, true), 3, -1);
            var pickUp = State(sm, "PickUp", Ph("Roulette_PickUp", T.RoulettePickUp, false), 3, 1);
            var aim = State(sm, "Aim", Ph("Roulette_Aim", T.RouletteAim, false), 4, 1);
            var hold = State(sm, "Hold", Ph("Roulette_Hold", T.RouletteHold, false), 5, 1);
            var relief = State(sm, "Relief", Ph("Roulette_Relief", T.RouletteRelief, false), 6, 0);
            var putDown = State(sm, "PutDown", Ph("Roulette_PutDown", T.RoulettePutDown, false), 7, 0);
            var death = State(sm, "Death", Ph("Roulette_Death", T.RouletteDeath, false), 6, 2);
            var deadLoop = State(sm, "DeadLoop", Ph("DeadLoop", 2f, true), 7, 2);
            var victory = State(sm, "Victory", Ph("Victory", T.Victory, true), -1, 2);
            sm.defaultState = idle;

            // 차례
            Go(idle, turnIdle, 0.25f).If(AnimIds.Character.IsTurn);
            Go(turnIdle, idle, 0.25f).IfNot(AnimIds.Character.IsTurn);

            // 리액션 (끝나면 Idle로, 차례면 Idle → TurnIdle로 바로 넘어간다)
            Any(sm, playCards, Blend).If(AnimIds.Character.PlayCards);
            Any(sm, callLiar, Blend).If(AnimIds.Character.CallLiar);
            Any(sm, accused, Blend).If(AnimIds.Character.Accused);
            Any(sm, revealWin, Blend).If(AnimIds.Character.RevealWin);
            Any(sm, revealLose, Blend).If(AnimIds.Character.RevealLose);
            foreach (var s in new[] { playCards, callLiar, accused, revealWin }) Exit(s, idle, 0.2f);
            Exit(revealLose, tense, 0.2f);

            // 룰렛 — 결과(Survived)는 시작 전에 이미 세팅되어 있다
            Any(sm, pickUp, 0.2f).If(AnimIds.Character.Roulette);
            Exit(pickUp, aim, Blend);
            Exit(aim, hold, Blend);
            Exit(hold, relief, 0.05f).If(AnimIds.Character.Survived);
            Exit(hold, death, 0.05f).IfNot(AnimIds.Character.Survived);
            Exit(relief, putDown, Blend);
            Exit(putDown, idle, 0.25f);
            Exit(death, deadLoop, 0.2f);

            Any(sm, deadLoop, 0.3f).If(AnimIds.Character.ForceDead);
            Any(sm, victory, 0.3f).If(AnimIds.Character.Victory);
            Any(sm, idle, 0f).If(AnimIds.Character.Reset);

            // 상태 신호 — 카메라 · 사운드 · 리볼버 타이밍을 Animator 흐름이 정한다
            Cue(aim, SeatCue.CameraFocus, SeatCue.None);
            Cue(hold, SeatCue.HeartbeatOn, SeatCue.HeartbeatOff);
            Cue(relief, SeatCue.RevolverClick, SeatCue.None);
            Cue(putDown, SeatCue.None, SeatCue.CameraRelease);
            Cue(death, SeatCue.RevolverFire, SeatCue.None);
            Cue(deadLoop, SeatCue.DeadSettled, SeatCue.None);
        }

        // ═════════════════════ 도우미 ═════════════════════

        enum P { Trigger, Bool, Int, Float }

        static AnimatorController NewController(string name)
        {
            string path = $"{Root}/{name}.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            {
                if (!EditorUtility.DisplayDialog("LiarsBar Animator",
                        $"{name}.controller가 이미 있습니다. 새로 만들까요?\n(클립은 유지, 컨트롤러에서 직접 바꾼 전이는 사라집니다)",
                        "새로 만들기", "건너뛰기"))
                    return null;
                AssetDatabase.DeleteAsset(path);
            }
            return AnimatorController.CreateAnimatorControllerAtPath(path);
        }

        static void Params(AnimatorController c, params (string name, P type)[] ps)
        {
            foreach (var (n, t) in ps)
                c.AddParameter(n, t switch
                {
                    P.Trigger => AnimatorControllerParameterType.Trigger,
                    P.Bool => AnimatorControllerParameterType.Bool,
                    P.Int => AnimatorControllerParameterType.Int,
                    _ => AnimatorControllerParameterType.Float,
                });
        }

        static AnimatorState State(AnimatorStateMachine sm, string name, Motion m, float x, float y)
        {
            var s = sm.AddState(name, new Vector3(300 + x * 230, 120 + y * 80));
            s.motion = m;
            s.writeDefaultValues = false;
            return s;
        }

        /// <summary>조건 전이 (종료 시간 없음)</summary>
        static AnimatorStateTransition Go(AnimatorState from, AnimatorState to, float dur)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = dur;
            return t;
        }

        /// <summary>클립이 끝나면 넘어가는 전이</summary>
        static AnimatorStateTransition Exit(AnimatorState from, AnimatorState to, float dur)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = 1f;
            t.hasFixedDuration = true;
            t.duration = dur;
            return t;
        }

        static AnimatorStateTransition Any(AnimatorStateMachine sm, AnimatorState to, float dur)
        {
            var t = sm.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = dur;
            t.canTransitionToSelf = false;
            return t;
        }

        static AnimatorStateTransition If(this AnimatorStateTransition t, string p) { t.AddCondition(AnimatorConditionMode.If, 0, p); return t; }
        static AnimatorStateTransition IfNot(this AnimatorStateTransition t, string p) { t.AddCondition(AnimatorConditionMode.IfNot, 0, p); return t; }

        static void Cue(AnimatorState s, SeatCue enter, SeatCue exit)
        {
            var b = s.AddStateMachineBehaviour<SeatCueBehaviour>();
            b.onEnter = enter;
            b.onExit = exit;
        }

        // ─── 클립 ───

        static string Folder(string name)
        {
            string path = $"{Root}/Clips/{name}";
            EnsureFolder(path);
            return path;
        }

        /// <summary>이미 있으면 그대로 돌려준다 (사용자가 다듬은 클립 보존)</summary>
        static AnimationClip Clip(string dir, string name, bool loop, Action<AnimationClip> fill)
        {
            string path = $"{dir}/{name}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null) return existing;

            var clip = new AnimationClip { name = name, frameRate = 60f };
            fill(clip);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        const float PoseLen = 0.1f;

        static void Float(AnimationClip k, string path, Type type, string prop, AnimationCurve curve) =>
            AnimationUtility.SetEditorCurve(k, EditorCurveBinding.FloatCurve(path, type, prop), curve);

        static void Const(AnimationClip k, string path, Type type, string prop, float v, float len = PoseLen) =>
            Float(k, path, type, prop, AnimationCurve.Constant(0f, len, v));

        static void Active(AnimationClip k, string path, bool on) =>
            Stepped(k, path, (0f, on ? 1f : 0f), (PoseLen, on ? 1f : 0f));

        static void Stepped(AnimationClip k, string path, params (float t, float v)[] keys)
        {
            var curve = new AnimationCurve();
            foreach (var (t, v) in keys) curve.AddKey(new Keyframe(t, v));
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            }
            Float(k, path, typeof(GameObject), "m_IsActive", curve);
        }

        /// <summary>localPosition. y만 from→to, x·z는 0 고정 (len 0이면 포즈). peak가 있으면 중간에 솟았다가 내려온다.</summary>
        static void Pos(AnimationClip k, string path, float fromY, float toY, float len = PoseLen, float peak = 0f)
        {
            Float(k, path, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0f, len, 0f));
            Float(k, path, typeof(Transform), "m_LocalPosition.z", AnimationCurve.Constant(0f, len, 0f));
            var y = peak > 0f
                ? new AnimationCurve(new Keyframe(0f, fromY), new Keyframe(len * 0.5f, peak), new Keyframe(len, toY))
                : AnimationCurve.EaseInOut(0f, fromY, len, toY);
            Float(k, path, typeof(Transform), "m_LocalPosition.y", y);
        }

        /// <summary>localEulerAngles. z만 from→to, x·y는 0 고정</summary>
        static void Euler(AnimationClip k, string path, float fromZ, float toZ, float len = PoseLen)
        {
            Float(k, path, typeof(Transform), "localEulerAnglesRaw.x", AnimationCurve.Constant(0f, len, 0f));
            Float(k, path, typeof(Transform), "localEulerAnglesRaw.y", AnimationCurve.Constant(0f, len, 0f));
            Float(k, path, typeof(Transform), "localEulerAnglesRaw.z", AnimationCurve.EaseInOut(0f, fromZ, len, toZ));
        }

        static void Scale(AnimationClip k, string path, AnimationCurve curve)
        {
            foreach (var axis in new[] { "x", "y", "z" })
                Float(k, path, typeof(Transform), "m_LocalScale." + axis, curve);
        }

        /// <summary>캐릭터용 빈 클립. 존재하지 않는 경로라 아무것도 움직이지 않고 길이만 갖는다.</summary>
        static void Placeholder(AnimationClip k, float len) =>
            Float(k, "__ReplaceWithRealAnimation__", typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0f, len, 1f));

        static AnimationCurve EaseOut(float len) =>
            new(new Keyframe(0f, 0f, 2f / len, 2f / len), new Keyframe(len, 1f, 0f, 0f));
        static AnimationCurve EaseIn(float len) =>
            new(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(len, 1f, 2f / len, 2f / len));
        static AnimationCurve EaseInOut(float len) => AnimationCurve.EaseInOut(0f, 0f, len, 1f);
        static AnimationCurve Bump(float len, float height, float baseValue = 0f) =>
            new(new Keyframe(0f, baseValue), new Keyframe(len * 0.4f, baseValue + height), new Keyframe(len, baseValue));
        static AnimationCurve Pop(float len) =>
            new(new Keyframe(0f, 0f), new Keyframe(len * 0.6f, 1.15f), new Keyframe(len, 1f));

        static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{cur}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
#endif
