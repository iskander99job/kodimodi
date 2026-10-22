using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using UnityEditor;


public class showcase : MonoBehaviour
{
    public Transform character;
    public bool Rotate;
    public float RotationSpeed;

    public MeshRenderer[] A;
    public MeshRenderer[] B;
    public MeshRenderer[] C;
    public MeshRenderer[] D;
    public MeshRenderer[] E;

    public SkinnedMeshRenderer charSMR;
    Material[] bodyMats = new Material[4];
    public Material[] bootsMats;
    public Material[] shortsMats;
    public Material[] shirtsMats;
    public Material[] skinMats;

    //dependent materials
    public MeshRenderer[] ears;
    public MeshRenderer drawstrings;
    public Material fur;
    Material[] drawstringsMats = new Material[2];

    int currentAIndex;
    int currentBIndex;
    int currentCIndex;
    int currentDIndex;
    int currentEIndex;
    int currentShirtsIndex;
    int currentShortsIndex;
    int currentSkinIndex;
    int currentBootsIndex;

    public Animator anim;
    public Text animText;
    public string[] animNamesInOrder;
    int currentAnimIndex = 0;

    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0; i < A.Length; i++)
        {
            if (A[i] != null)
                A[i].enabled = false;
        }
        if (A[currentAIndex] != null)
            A[currentAIndex].enabled = true;

        for (int i = 0; i < B.Length; i++)
        {
            if (B[i] != null)
                B[i].enabled = false;
        }
        if (B[currentBIndex] != null)
            B[currentBIndex].enabled = true;

        for (int i = 0; i < C.Length; i++)
        {
            if (C[i] != null)
                C[i].enabled = false;
        }
        if (C[currentCIndex] != null)
            C[currentCIndex].enabled = true;

        for (int i = 0; i < D.Length; i++)
        {
            if (D[i] != null)
                D[i].enabled = false;
        }
        if (D[currentDIndex] != null)
            D[currentDIndex].enabled = true;

        for (int i = 0; i < E.Length; i++)
        {
            if (E[i] != null)
                E[i].enabled = false;
        }
        if (E[currentEIndex] != null)
            E[currentEIndex].enabled = true;

        bodyMats[0] = bootsMats[currentBootsIndex];
        bodyMats[1] = shortsMats[currentShortsIndex];
        bodyMats[2] = shirtsMats[currentShirtsIndex];
        bodyMats[3] = skinMats[currentSkinIndex];
        charSMR.materials = bodyMats;

        foreach (MeshRenderer m in ears)
        { 
            m.material = bodyMats[2]; 
        }
        drawstringsMats[0] = fur;
        drawstringsMats[1] = bodyMats[2];
        drawstrings.materials = drawstringsMats;

        anim.SetInteger("animIndex", currentAnimIndex);
        animText.text = animNamesInOrder[currentAnimIndex];
    }

    // Update is called once per frame
    void Update()
    {
        if (Rotate)
            character.Rotate(Vector3.up * RotationSpeed * Time.deltaTime);
    }
    public void rotate()
    {
        Rotate = Rotate ?  false  : true;
    }
    
    public void A_Button(int p)
    {
        if(currentAIndex + p > A.Length - 1)
        {
            if (A[currentAIndex] != null)
                A[currentAIndex].enabled = false;
            if (A[0] != null)
                A[0].enabled = true;
            currentAIndex = 0;
        }
        else if (currentAIndex + p < 0)
        {
            if (A[currentAIndex] != null)
                A[currentAIndex].enabled = false;
            if (A[A.Length - 1] != null)
                A[A.Length - 1].enabled = true;
            currentAIndex = A.Length - 1;
        }
        else
        {
            if (A[currentAIndex] != null)
                A[currentAIndex].enabled = false;
            if (A[currentAIndex + p] != null)
                A[currentAIndex + p].enabled = true;
            currentAIndex = currentAIndex + p;
        }
    }
    public void B_Button(int p)
    {
        if (currentBIndex + p > B.Length - 1)
        {
            if (B[currentBIndex] != null)
                B[currentBIndex].enabled = false;
            if (B[0] != null)
                B[0].enabled = true;
            currentBIndex = 0;
        }
        else if (currentBIndex + p < 0)
        {
            if (B[currentBIndex] != null)
                B[currentBIndex].enabled = false;
            if (B[B.Length - 1] != null)
                B[B.Length - 1].enabled = true;
            currentBIndex = B.Length - 1;
        }
        else
        {
            if (B[currentBIndex] != null)
                B[currentBIndex].enabled = false;
            if (B[currentBIndex + p] != null)
                B[currentBIndex + p].enabled = true;
            currentBIndex = currentBIndex + p;
        }
    }
    public void C_Button(int p)
    {
        if (currentCIndex + p > C.Length - 1)
        {
            if (C[currentCIndex] != null)
                C[currentCIndex].enabled = false;
            if (C[0] != null)
                C[0].enabled = true;
            currentCIndex = 0;
        }
        else if (currentCIndex + p < 0)
        {
            if (C[currentCIndex] != null)
                C[currentCIndex].enabled = false;
            if (C[C.Length - 1] != null)
                C[C.Length - 1].enabled = true;
            currentCIndex = C.Length - 1;
        }
        else
        {
            if (C[currentCIndex] != null)
                C[currentCIndex].enabled = false;
            if (C[currentCIndex + p] != null)
                C[currentCIndex + p].enabled = true;
            currentCIndex = currentCIndex + p;
        }
    }
    public void D_Button(int p)
    {
        if (currentDIndex + p > D.Length - 1)
        {
            if(D[currentDIndex] != null)
                D[currentDIndex].enabled = false;
            if (D[0] != null)
                D[0].enabled = true;
            currentDIndex = 0;
        }
        else if (currentDIndex + p < 0)
        {
            if (D[currentDIndex] != null)
                D[currentDIndex].enabled = false;
            if (D[D.Length - 1] != null)
                D[D.Length - 1].enabled = true;
            currentDIndex = D.Length - 1;
        }
        else
        {
            if (D[currentDIndex] != null)
                D[currentDIndex].enabled = false;
            if (D[currentDIndex + p] != null)
                D[currentDIndex + p].enabled = true;
            currentDIndex = currentDIndex + p;
        }
    }
    public void E_Button(int p)
    {
        if (currentEIndex + p > E.Length - 1)
        {
            if (E[currentEIndex] != null)
                E[currentEIndex].enabled = false;
            if (E[0] != null)
                E[0].enabled = true;
            currentEIndex = 0;
        }
        else if (currentEIndex + p < 0)
        {
            if (E[currentEIndex] != null)
                E[currentEIndex].enabled = false;
            if (E[D.Length - 1] != null)
                E[D.Length - 1].enabled = true;
            currentEIndex = E.Length - 1;
        }
        else
        {
            if (E[currentEIndex] != null)
                E[currentEIndex].enabled = false;
            if (E[currentEIndex + p] != null)
                E[currentEIndex + p].enabled = true;
            currentEIndex = currentEIndex + p;
        }
    }
    public void Shirt_Button(int p)
    {
        if (currentShirtsIndex + p > shirtsMats.Length - 1)
        {
            if (shirtsMats[0] != null)
                bodyMats[2] = shirtsMats[0];
            currentShirtsIndex = 0;
        }
        else if (currentShirtsIndex + p < 0)
        {
            if (shirtsMats[shirtsMats.Length - 1] != null)
                bodyMats[2] = shirtsMats[shirtsMats.Length - 1];
            currentShirtsIndex = shirtsMats.Length - 1;
        }
        else
        {
            if (shirtsMats[currentShirtsIndex + p] != null)
                bodyMats[2] = shirtsMats[currentShirtsIndex + p];
            currentShirtsIndex = currentShirtsIndex + p;
        }
        charSMR.materials = bodyMats;

        foreach (MeshRenderer m in ears)
            m.material = bodyMats[2];
        drawstringsMats[0] = fur;
        drawstringsMats[1] = bodyMats[2];
        drawstrings.materials = drawstringsMats;
    }
    public void Shorts_Button(int p)
    {
        if (currentShortsIndex + p > shortsMats.Length - 1)
        {
            if (shortsMats[0] != null)
                bodyMats[1] = shortsMats[0];
            currentShortsIndex = 0;
        }
        else if (currentShortsIndex + p < 0)
        {
            if (shortsMats[shortsMats.Length - 1] != null)
                bodyMats[1] = shortsMats[shortsMats.Length - 1];
            currentShortsIndex = shortsMats.Length - 1;
        }
        else
        {
            if (shortsMats[currentShortsIndex + p] != null)
                bodyMats[1] = shortsMats[currentShortsIndex + p];
            currentShortsIndex = currentShortsIndex + p;
        }
        charSMR.materials = bodyMats;
    }
    public void Skin_Button(int p)
    {
        if (currentSkinIndex + p > skinMats.Length - 1)
        {
            if (skinMats[0] != null)
                bodyMats[3] = skinMats[0];
            currentSkinIndex = 0;
        }
        else if (currentSkinIndex + p < 0)
        {
            if (skinMats[skinMats.Length - 1] != null)
                bodyMats[3] = skinMats[skinMats.Length - 1];
            currentSkinIndex = skinMats.Length - 1;
        }
        else
        {
            if (skinMats[currentSkinIndex + p] != null)
                bodyMats[3] = skinMats[currentSkinIndex + p];
            currentSkinIndex = currentSkinIndex + p;
        }
        charSMR.materials = bodyMats;
    }
    public void Boots_Button(int p)
    {
        if (currentBootsIndex + p > bootsMats.Length - 1)
        {
            if (bootsMats[0] != null)
                bodyMats[0] = bootsMats[0];
            currentBootsIndex = 0;
        }
        else if (currentBootsIndex + p < 0)
        {
            if (bootsMats[bootsMats.Length - 1] != null)
                bodyMats[0] = bootsMats[bootsMats.Length - 1];
            currentBootsIndex = bootsMats.Length - 1;
        }
        else
        {
            if (bootsMats[currentBootsIndex + p] != null)
                bodyMats[0] = bootsMats[currentBootsIndex + p];
            currentBootsIndex = currentBootsIndex + p;
        }
        charSMR.materials = bodyMats;
    }
    public void variant_Button(string layer)
    {
        switch(layer)
        {
            case "A":
                A[currentAIndex].GetComponent<variant>().Change();
                break;
            case "B":
                B[currentBIndex].GetComponent<variant>().Change();
                break;
            case "C":
                C[currentCIndex].GetComponent<variant>().Change();
                break;
            case "D":
                D[currentDIndex].GetComponent<variant>().Change();
                break;
            case "E":
                E[currentEIndex].GetComponent<variant>().Change();
                break;
        }
    }
    public void shuffle_Button()
    {
        int r;
        if (A[currentAIndex] != null)
            A[currentAIndex].enabled = false;
        r = Random.Range(0, A.Length);
        if (A[r] != null)
            A[r].enabled = true;
        currentAIndex = r;

        if (B[currentBIndex] != null)
            B[currentBIndex].enabled = false;
        r = Random.Range(0, B.Length);
        if (B[r] != null)
            B[r].enabled = true;
        currentBIndex = r;

        if (C[currentCIndex] != null)
            C[currentCIndex].enabled = false;
        r = Random.Range(0, C.Length);
        if (C[r] != null)
            C[r].enabled = true;
        currentCIndex = r;

        if (D[currentDIndex] != null)
            D[currentDIndex].enabled = false;
        r = Random.Range(0, D.Length);
        if (D[r] != null)
            D[r].enabled = true;
        currentDIndex = r;

        if (E[currentEIndex] != null)
            E[currentEIndex].enabled = false;
        r = Random.Range(0, E.Length);
        if (E[r] != null)
            E[r].enabled = true;
        currentEIndex = r;

        r = Random.Range(0, bootsMats.Length);
        if (bootsMats[r] != null)
            bodyMats[0] = bootsMats[r];
        currentBootsIndex = r;

        r = Random.Range(0, shortsMats.Length);
        if (shortsMats[r] != null)
            bodyMats[1] = shortsMats[r];
        currentShortsIndex = r;

        r = Random.Range(0, shirtsMats.Length);
        if (shirtsMats[r] != null)
            bodyMats[2] = shirtsMats[r];
        currentShirtsIndex = r;

        r = Random.Range(0, skinMats.Length);
        if (skinMats[r] != null)
            bodyMats[3] = skinMats[r];
        currentSkinIndex = r;

        charSMR.materials = bodyMats;

        foreach (MeshRenderer m in ears)
            m.material = bodyMats[2];
        drawstringsMats[0] = fur;
        drawstringsMats[1] = bodyMats[2];
        drawstrings.materials = drawstringsMats;
    }
    public void Anim_Button(int p)
    {
        if (currentAnimIndex + p > animNamesInOrder.Length - 1)
        {
            anim.SetInteger("animIndex", 0);
            animText.text = animNamesInOrder[0];
            currentAnimIndex = 0;
        }
        else if (currentAnimIndex + p < 0)
        {
            anim.SetInteger("animIndex", animNamesInOrder.Length - 1);
            animText.text = animNamesInOrder[animNamesInOrder.Length - 1];
            currentAnimIndex = animNamesInOrder.Length - 1;
        }
        else
        {
            anim.SetInteger("animIndex", currentAnimIndex + p);
            animText.text = animNamesInOrder[currentAnimIndex + p];
            currentAnimIndex = currentAnimIndex + p;
        }
    }

    public void savePrefab()
    {
        // string locationPath = "Assets/FEMOR/Low Poly Custom Characters/Prefabs/saved custom characters/" + character.name + ".prefab";
        // locationPath = AssetDatabase.GenerateUniqueAssetPath(locationPath);
        // PrefabUtility.SaveAsPrefabAssetAndConnect(character.gameObject, locationPath, InteractionMode.UserAction);
    }
}
