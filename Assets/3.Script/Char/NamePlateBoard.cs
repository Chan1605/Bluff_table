using UnityEngine;

public class NamePlateBoard : MonoBehaviour
{
    public static NamePlateBoard Instance { get; private set; }

    [SerializeField] private int seatCount = 4;

    NamePlate[] plates;

    string[] names;
    int[] shots;
    bool[] dead;
    bool[] visible;
    int turn = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("NamePlateBoard가 씬에 두 개 이상 있습니다.", this);
        Instance = this;

        plates = new NamePlate[seatCount];
        names = new string[seatCount];
        shots = new int[seatCount];
        dead = new bool[seatCount];
        visible = new bool[seatCount];
        for (int i = 0; i < seatCount; i++) visible[i] = true;

        foreach (var p in FindObjectsByType<NamePlate>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Register(p);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }


    public void Register(NamePlate plate)
    {
        int seat = plate.Seat;
        if (!IsValid(seat)) return;

        plates[seat] = plate;
        Apply(seat);
    }

    public void Unregister(NamePlate plate)
    {
        int seat = plate.Seat;
        if (IsValid(seat) && plates[seat] == plate)
            plates[seat] = null;
    }
    public void SetName(int seat, string nick)
    {
        if (!IsValid(seat)) return;
        names[seat] = nick;
        if (TryGet(seat, out var p)) p.SetName(nick);
    }

    public void SetShots(int seat, int fired)
    {
        if (!IsValid(seat)) return;
        shots[seat] = fired;
        if (TryGet(seat, out var p)) p.SetShots(fired);
    }

    public void SetDead(int seat, bool isDead)
    {
        if (!IsValid(seat)) return;
        dead[seat] = isDead;
        if (TryGet(seat, out var p)) p.SetDead(isDead);
    }

    public void SetTurn(int seat)
    {
        turn = seat;
        for (int i = 0; i < seatCount; i++)
            if (TryGet(i, out var p)) p.SetTurn(i == seat);
    }

    public void ShowSeats(int[] seats)
    {
        for (int i = 0; i < seatCount; i++)
        {
            visible[i] = System.Array.IndexOf(seats, i) >= 0;
            if (TryGet(i, out var p)) p.gameObject.SetActive(visible[i]);
        }
    }

    void Apply(int seat)
    {
        if (!TryGet(seat, out var p)) return;

        if (names[seat] != null) p.SetName(names[seat]);
        p.SetShots(shots[seat]);
        p.SetDead(dead[seat]);
        p.SetTurn(seat == turn);
        p.gameObject.SetActive(visible[seat]);
    }

    bool IsValid(int seat) => seat >= 0 && seat < seatCount;

    bool TryGet(int seat, out NamePlate plate)
    {
        plate = plates[seat];
        return plate != null;
    }
}