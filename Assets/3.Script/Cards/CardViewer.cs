using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardViewer : MonoBehaviour
{

    [SerializeField] private Material ShownMat, hiddenMat;

    [Tooltip("CardType 순서대로: King, Queen, Ace, Joker")]
    [SerializeField] Mesh[] typeMeshes = new Mesh[4];
    [SerializeField] Mesh hiddenMesh;

    public int Index { get; set; } = -1;
    public bool IsRevealed { get; private set; }

    private MeshFilter faceFilter;
    private MeshRenderer faceRenderer;

    private void Awake()
    {
        faceFilter = GetComponentInChildren<MeshFilter>();
        faceRenderer = GetComponentInChildren<MeshRenderer>();
    }
    public void SetFace(CardType type)
    {
        IsRevealed = true;
        faceFilter.sharedMesh = typeMeshes[(int)type];
        faceRenderer.sharedMaterial = ShownMat;
    }

    public void SetHidden()
    {
        IsRevealed = false;
        faceRenderer.sharedMaterial = hiddenMat;
    }
}
