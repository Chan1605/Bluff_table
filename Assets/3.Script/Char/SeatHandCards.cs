using System.Collections.Generic;
using UnityEngine;
using LiarsBar.Presentation;

// 앉은 캐릭터 한 명의 손패.
// 배분 · 제출 때 날아가는 카드는 HandView(민규님) + 런타임 CardView 5장,
// 손에 쥔 모습은 모델에 원래 있는 왼손 Kartlar/kart1~5를 켜고 메쉬만 바꿔서 보여 준다 (원본 그립 그대로).
// HandCardsSetup.Attach가 캐릭터 생성 직후 붙인다. 캐릭터가 지워지면 카드도 같이 지운다.
public class SeatHandCards : MonoBehaviour
{
    // 캐릭터 Animator 파라미터 (없으면 호출 생략)
    static readonly int ReceiveCardsId = Animator.StringToHash("ReceiveCards");   // 배분 끝 → 테이블에서 패 줍기 (PickUp)
    static readonly int OpenCardsId = Animator.StringToHash("OpenCards");         // 스페이스 → 패 펼치기 (Open)
    static readonly int CloseCardsId = Animator.StringToHash("CloseCards");       // 스페이스 → 다시 덮기 (Closed)
    static readonly int HasCardsId = Animator.StringToHash("HasCards");
    static readonly int PlayCardsId = AnimIds.Character.PlayCardsId;

    const string KartsParentName = "KartsParent";
    const string KartRootName = "Kartlar";
    const string KartPrefix = "kart";
    const int KartCount = 7;   // 모델에는 kart1~7이 있고 손패는 1~5만 쓴다

    struct CardFace
    {
        public MeshFilter filter;
        public MeshRenderer renderer;
        public Mesh defaultMesh;
    }

    // 모델에 원래 있는 손 카드 한 장
    class Kart
    {
        public Transform transform;
        public Renderer renderer;         // MeshRenderer 또는 SkinnedMeshRenderer
        public MeshFilter filter;         // MeshRenderer일 때
        public Mesh defaultMesh;
        public Material[] defaultMaterials;
        public Renderer[] extras;         // number 등 자식 렌더러 (항상 숨김)
        public Vector3 basePosition;      // Animator가 쓴 값 (들어 올림 전)
        public Vector3 lastWritten;
        public bool hasBase;
        public bool usingDefaultMaterials = true;
    }

    public int Seat { get; private set; }
    public bool IsLocal { get; private set; }
    public HandView Hand { get; private set; }
    public bool IsDead => state != null && state.isDead;

    HandCardsSetup setup;
    Transform kartRootT;              // 손 카드 묶음 (KartsParent/Kartlar)
    Quaternion kartRootBaseRotation;  // 배치 복사 후 기본 회전 (보정 회전은 여기에 더한다)
    PlayerState state;
    Animator character;
    GameObject cardsRoot;

    readonly Transform[] slots = new Transform[HandView.HandSize];
    readonly Kart[] karts = new Kart[HandView.HandSize];
    readonly float[] dealtAt = new float[HandView.HandSize];
    readonly bool[] lifted = new bool[HandView.HandSize];
    readonly Dictionary<CardView, CardFace> faces = new();
    readonly HashSet<int> characterParams = new();

    public bool Build(HandCardsSetup owner, int seat, bool isLocal, PlayerState playerState)
    {
        setup = owner;
        Seat = seat;
        IsLocal = isLocal;
        state = playerState;

        // 1) 모델의 kart1~7: 1~5는 손패 표시용, 6~7은 항상 숨김. 처음엔 모두 끈다
        // 모델에는 Kartlar가 여러 개 있다 (MasaKart/Kartlar = 테이블 카드 등). 왼손 KartsParent 밑의 것을 쓴다
        Transform handParent = FindDeep(transform, KartsParentName);
        Transform kartRoot = handParent != null ? FindDeep(handParent, KartRootName) : null;
        if (kartRoot == null)
        {
            Debug.LogWarning($"[손패] {name}에서 {KartsParentName}/{KartRootName}을 못 찾아 손패 생략 (좌석 {seat})", this);
            return false;
        }
        // 프리팹에서 KartsParent · Kartlar가 꺼져 있을 수 있다. 손 카드까지 경로를 모두 켜고, 표시는 렌더러로 켜고 끈다
        for (Transform p = kartRoot; p != null && p != transform; p = p.parent)
            if (!p.gameObject.activeSelf) p.gameObject.SetActive(true);
        CopyHandLayout(kartRoot);
        kartRootT = kartRoot;
        kartRootBaseRotation = kartRoot.localRotation;
        Transform searchRoot = kartRoot;
        for (int n = 1; n <= KartCount; n++)
        {
            Transform t = FindDeep(searchRoot, KartPrefix + n);
            if (t == null)
            {
                if (n <= HandView.HandSize)
                {
                    Debug.LogWarning($"[손패] {name}에서 {KartPrefix}{n}을 못 찾아 손패 생략 (좌석 {seat})", this);
                    return false;
                }
                continue;
            }

            t.gameObject.SetActive(true);
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            if (n > HandView.HandSize) continue;

            var kart = new Kart { transform = t, renderer = t.GetComponent<Renderer>() };
            if (kart.renderer == null)
            {
                Debug.LogWarning($"[손패] {t.name}에 Renderer가 없어 손패 생략 (좌석 {seat})", this);
                return false;
            }
            kart.filter = t.GetComponent<MeshFilter>();
            kart.defaultMesh = kart.filter != null ? kart.filter.sharedMesh
                             : (kart.renderer as SkinnedMeshRenderer)?.sharedMesh;
            kart.defaultMaterials = kart.renderer.sharedMaterials;
            var extras = new List<Renderer>();
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) if (r != kart.renderer) extras.Add(r);
            kart.extras = extras.ToArray();
            karts[n - 1] = kart;

            // 날아가는 카드의 도착점
            var slot = new GameObject("HandSlot").transform;
            slot.SetParent(t, false);
            slots[n - 1] = slot;
        }

        // 2) 캐릭터 Animator 파라미터 캐시
        character = GetComponent<Animator>();
        if (character == null) character = GetComponentInChildren<Animator>();
        if (character != null)
            foreach (var p in character.parameters) characterParams.Add(p.nameHash);

        // 3) 날아가는 카드 5장 + HandView. 비활성 상태에서 연결한 뒤 켠다 (HandView · CardView의 Awake가 연결 후에 돌도록)
        cardsRoot = new GameObject($"HandCards (seat {seat})");
        cardsRoot.SetActive(false);
        var cards = new CardView[HandView.HandSize];
        for (int i = 0; i < cards.Length; i++) cards[i] = CreateCard(i, cardsRoot.transform);

        Hand = cardsRoot.AddComponent<HandView>();
        Hand.Bind(slots, cards);
        Hand.Setup(isLocal, null);   // 앞면은 재질이 아니라 메쉬 교체라 RefreshFaces에서 처리
        cardsRoot.SetActive(true);
        DontDestroyOnLoad(cardsRoot); // 테이블 씬이 바뀌어도 캐릭터(플레이어 자식)와 수명을 같이한다

        ApplySlotOffsets();
        ApplyKartlarFix();
        return true;
    }

    CardView CreateCard(int index, Transform parent)
    {
        // CardView 계층 (클립 바인딩 경로와 같아야 함): Card / Visual / Flip / Back · Face · GlowMatch · GlowMiss
        var root = new GameObject($"Card{index + 1}");
        root.transform.SetParent(parent, false);
        root.transform.localScale = Vector3.one * setup.CardScale;

        Transform visual = NewChild(root.transform, "Visual");
        visual.gameObject.SetActive(false);   // 기본 상태(Hidden)와 같게. 첫 프레임에 원점에서 번쩍이지 않도록
        Transform flip = NewChild(visual, "Flip");
        NewChild(flip, "Back");
        NewChild(flip, "GlowMatch").gameObject.SetActive(false);
        NewChild(flip, "GlowMiss").gameObject.SetActive(false);

        GameObject face = Instantiate(setup.CardVisualPrefab, flip, false);
        face.name = "Face";
        foreach (var viewer in face.GetComponentsInChildren<CardViewer>(true)) Destroy(viewer);
        foreach (var col in face.GetComponentsInChildren<Collider>(true)) col.enabled = false;

        var animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = setup.CardController;
        animator.applyRootMotion = false;
        var view = root.AddComponent<CardView>();

        var filter = face.GetComponentInChildren<MeshFilter>(true);
        faces[view] = new CardFace
        {
            filter = filter,
            renderer = face.GetComponentInChildren<MeshRenderer>(true),
            defaultMesh = filter != null ? filter.sharedMesh : null
        };
        return view;
    }

    void Update()
    {
        if (setup == null || Hand == null) return;
        // 플레이 중 Inspector에서 오프셋을 바꿔도 바로 보이게 매 프레임 적용 (Animator보다 먼저 돈다)
        ApplySlotOffsets();
        ApplyKartlarFix();
    }

    // Animator가 kart를 움직인 뒤에 표시 · 들어 올림을 정한다
    void LateUpdate()
    {
        if (setup == null || Hand == null) return;

        for (int i = 0; i < karts.Length; i++)
        {
            Kart kart = karts[i];
            if (kart == null) continue;
            CardView card = Hand.Cards[i];

            // 손에 닿았으면 모델의 kart로 보여 주고, 날아가는 카드는 숨긴다
            bool settled = card.InHand && Time.time - dealtAt[i] >= PresentationTimings.CardDealFlight;
            kart.renderer.enabled = settled;
            foreach (var r in kart.extras) if (r != null) r.enabled = false;
            if (faces.TryGetValue(card, out CardFace f) && f.renderer != null) f.renderer.enabled = !settled;

            if (settled) ApplyKartFace(kart, card.Rank);
            ApplyLift(kart, settled && lifted[i]);
        }
    }

    void OnDestroy()
    {
        if (cardsRoot != null) Destroy(cardsRoot);
        if (setup != null) setup.Unregister(this);
    }

    // ───── HandCardsSetup이 호출 ─────

    public bool AnyVisible
    {
        get
        {
            foreach (var c in Hand.Cards) if (c.IsVisible) return true;
            return false;
        }
    }

    public int InHandCount
    {
        get
        {
            int n = 0;
            foreach (var c in Hand.Cards) if (c.InHand) n++;
            return n;
        }
    }

    public void DealOne(int slot, Transform deck)
    {
        Hand.DealOne(slot, deck);
        dealtAt[slot] = Time.time;
        lifted[slot] = false;
        RefreshFaces();
    }

    public void SetLifted(IReadOnlyList<int> slotIndices)
    {
        for (int i = 0; i < lifted.Length; i++) lifted[i] = false;
        if (slotIndices != null)
            foreach (int s in slotIndices) if (s >= 0 && s < lifted.Length) lifted[s] = true;
        Hand.SetLifted(slotIndices ?? new List<int>());
    }

    /// <summary>서버 손패 인덱스(남은 패 기준 0~n-1) → 슬롯 번호(0~4). 낸 카드는 슬롯에서 빠지므로 손에 남은 순서로 센다.</summary>
    public List<int> ListToSlots(IReadOnlyList<int> listIndices)
    {
        var inHand = new List<int>();
        for (int i = 0; i < Hand.Cards.Count; i++) if (Hand.Cards[i].InHand) inHand.Add(i);

        var result = new List<int>();
        if (listIndices == null) return result;
        foreach (int li in listIndices)
            if (li >= 0 && li < inHand.Count) result.Add(inHand[li]);
        return result;
    }

    bool dealFinished;
    bool opened;

    /// <summary>패를 다 받았고 손에 남아 있으면 true (스페이스로 펼치기 ↔ 덮기)</summary>
    public bool CanToggle => dealFinished && InHandCount > 0;
    public bool IsOpen => opened;

    public void ResetRound()
    {
        dealFinished = false;
        opened = false;
    }

    // 배분이 끝나면 패를 줍고(PickUp) 덮은 채 쥐고 기다린다(Closed). 펼치기는 스페이스
    public void OnDealFinished()
    {
        dealFinished = true;
        SetHasCards(InHandCount > 0);
        TriggerCharacter(ReceiveCardsId);
    }

    // 원작 클립(kart_atma)에 들어 있는 애니메이션 이벤트. 받는 함수가 없으면 'has no receiver' 오류가 난다.
    // 카드가 손을 떠나는 순간이라, 나중에 제출 카드 날리는 타이밍을 여기에 맞출 수 있다
    public void ThrowAnim() { }
    public void OnThrowTableCardsEvent() { }

    public void SetCardsOpen(bool open)
    {
        if (opened == open) return;
        opened = open;
        TriggerCharacter(open ? OpenCardsId : CloseCardsId);   // Open(kart_acma) → HoldCards / Closed(kart_el_masa)
    }

    public void PlayCards(int count, int[] listIndices, HandCardsSetup table)
    {
        int[] slotIndices = ListToSlots(listIndices).ToArray();   // 꺼내기 전에 계산해야 순서가 맞다
        foreach (var card in Hand.TakeForPlay(count, slotIndices))
        {
            card.ClearFace();                    // 테이블에는 뒷면으로
            card.PlayTo(table.NextPileSlot());
            lifted[card.Index] = false;
        }
        RefreshFaces();
        TriggerCharacter(PlayCardsId);
        SetHasCards(InHandCount > 0);
    }

    /// <summary>제출 후 서버가 다시 보내 준 남은 손패로 앞면을 맞춘다</summary>
    public void ResyncLocal(List<CardType> hand)
    {
        int k = 0;
        foreach (var card in Hand.Cards)
        {
            if (!card.InHand) continue;
            if (k < hand.Count) Hand.ApplyFace(card, (CardRank)hand[k]);
            k++;
        }
        if (k != hand.Count)
            Debug.LogWarning($"[손패] 좌석 {Seat}: 손에 든 카드 {k}장 ≠ 서버 손패 {hand.Count}장");
        RefreshFaces();
    }

    /// <summary>날아가는 카드의 앞면 (CardViewer와 같은 방식: 메쉬 · 재질 교체)</summary>
    public void RefreshFaces()
    {
        foreach (var kv in faces)
        {
            CardFace f = kv.Value;
            if (f.renderer == null || f.filter == null) continue;

            CardRank? rank = kv.Key.Rank;
            Mesh faceMesh = IsLocal && rank.HasValue ? setup.GetTypeMesh((int)rank.Value) : null;
            if (faceMesh != null)
            {
                f.filter.sharedMesh = faceMesh;
                f.renderer.sharedMaterial = setup.ShownMat;
            }
            else
            {
                if (f.defaultMesh != null) f.filter.sharedMesh = f.defaultMesh;
                f.renderer.sharedMaterial = setup.HiddenMat;
            }
        }
    }

    public void SetHasCards(bool on)
    {
        if (character != null && characterParams.Contains(HasCardsId)) character.SetBool(HasCardsId, on);
    }

    void TriggerCharacter(int id)
    {
        if (character != null && characterParams.Contains(id)) character.SetTrigger(id);
    }

    // ───── 내부 ─────

    // 손에 쥔 카드: 내 패면 해당 종류 메쉬 + 원래 재질, 남의 패면 원래 메쉬 + 뒷면 재질
    // (CardViewer의 typeMeshes가 캐릭터 FBX의 kart 메쉬라 손 위치 · 방향이 그대로 맞는다)
    void ApplyKartFace(Kart kart, CardRank? rank)
    {
        Mesh mesh = kart.defaultMesh;
        bool showFace = IsLocal && rank.HasValue;
        if (showFace)
        {
            Mesh typeMesh = setup.GetTypeMesh((int)rank.Value);
            if (typeMesh != null) mesh = typeMesh;
        }

        if (kart.filter != null) { if (kart.filter.sharedMesh != mesh) kart.filter.sharedMesh = mesh; }
        else if (kart.renderer is SkinnedMeshRenderer smr && smr.sharedMesh != mesh) smr.sharedMesh = mesh;

        bool useDefault = showFace || setup.HiddenMat == null;
        if (kart.usingDefaultMaterials != useDefault)
        {
            kart.usingDefaultMaterials = useDefault;
            if (useDefault) kart.renderer.sharedMaterials = kart.defaultMaterials;
            else kart.renderer.sharedMaterial = setup.HiddenMat;
        }
    }

    // 선택한 카드 들어 올리기. Animator가 kart를 움직이든 안 움직이든 원래 위치에 더하는 방식
    void ApplyLift(Kart kart, bool on)
    {
        Transform t = kart.transform;
        if (!kart.hasBase || t.localPosition != kart.lastWritten)
        {
            kart.basePosition = t.localPosition;   // Animator(또는 처음 값)가 새로 쓴 위치
            kart.hasBase = true;
        }

        Vector3 target = kart.basePosition;
        if (on && t.parent != null)
        {
            Vector3 worldOffset = t.TransformDirection(setup.LiftAxis).normalized * setup.LiftDistance;
            target += t.parent.InverseTransformVector(worldOffset);
        }
        t.localPosition = target;
        kart.lastWritten = target;
    }

    // 기준 캐릭터의 Kartlar · kart1~7 로컬 배치를 복사한다 (메쉬는 모든 캐릭터가 같아서 배치만 맞추면 쥐는 모양이 같아진다)
    void CopyHandLayout(Transform target)
    {
        var animator = GetComponent<Animator>();
        GameObject reference = setup.GetHandLayoutReference(animator != null ? animator.runtimeAnimatorController : null);
        if (reference == null || reference.name == name.Replace("(Clone)", "").Trim()) return;   // 기준 없음 · 자기 자신
        Transform refParent = FindDeep(reference.transform, KartsParentName);
        Transform refRoot = refParent != null ? FindDeep(refParent, KartRootName) : null;
        if (refRoot == null)
        {
            Debug.LogWarning($"[손패] 기준 캐릭터 {reference.name}에 {KartsParentName}/{KartRootName}이 없어 배치 복사 생략", this);
            return;
        }

        CopyLocal(refRoot, target);
        for (int n = 1; n <= KartCount; n++)
        {
            Transform from = FindDeep(refRoot, KartPrefix + n);
            Transform to = FindDeep(target, KartPrefix + n);
            if (from != null && to != null) CopyLocal(from, to);
        }
    }

    static void CopyLocal(Transform from, Transform to)
    {
        to.localPosition = from.localPosition;
        to.localRotation = from.localRotation;
        to.localScale = from.localScale;
    }

    // 컨트롤러별 손 카드 묶음 회전 보정 (플레이 중 Inspector에서 바꿔도 반영)
    void ApplyKartlarFix()
    {
        if (kartRootT == null) return;
        var animator = GetComponent<Animator>();
        Vector3 euler = setup.GetKartlarEuler(animator != null ? animator.runtimeAnimatorController : null);
        kartRootT.localRotation = kartRootBaseRotation * Quaternion.Euler(euler);
    }

    void ApplySlotOffsets()
    {
        Quaternion rot = Quaternion.Euler(setup.SlotEulerOffset);
        Vector3 offset = setup.SlotPositionOffset;
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            // kart의 큰 FBX 스케일을 상쇄해서 오프셋을 m 단위로 쓴다
            Vector3 s = slot.parent.lossyScale;
            slot.localPosition = new Vector3(SafeDiv(offset.x, s.x), SafeDiv(offset.y, s.y), SafeDiv(offset.z, s.z));
            slot.localRotation = rot;
        }
    }

    static float SafeDiv(float v, float s) => Mathf.Abs(s) < 1e-6f ? 0f : v / s;

    static Transform NewChild(Transform parent, string childName)
    {
        var t = new GameObject(childName).transform;
        t.SetParent(parent, false);
        return t;
    }

    static Transform FindDeep(Transform root, string target)
    {
        if (root.name == target) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, target);
            if (found != null) return found;
        }
        return null;
    }
}
