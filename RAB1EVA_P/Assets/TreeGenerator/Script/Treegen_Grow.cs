/* -------------------------------------------------------------------------
 * Copyright © 2026Dave Berg. All rights reserved.
 * 
 * File: TreeGen_Grow.cs
 * 
 * ------------------------------------------------------------------------- */

using System.Collections;
using UnityEngine;

public class Treegen_Grow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Drag the Tree Prefab from your project folder here. The script will spawn it and make it grow.")]
    public GameObject treePrefab;

    [Header("Growth Settings")]
    public bool startAtGameStart = true;
    public bool setGrowAtStartTo0 = true;
    public float startDelayInSeconds = 2.0f;
    public float growthTimePeriod = 5.0f;
    [Tooltip("Multiplier for the growth speed. 1 is normal speed.")]
    public float speed = 1.0f;

    [Header("Shader Properties")]
    [Tooltip("The exact property name of the growth slider in your Unity material.")]
    public string growthPropertyName = "_GrowPhase"; 

    private GameObject treeInstance;
    private Renderer[] targetRenderers;
    private int growthPropertyID;
    private MaterialPropertyBlock propBlock;

    void Start()
    {
        // 1. Instantiate (Spawn) the prefab if one is assigned
        if (treePrefab != null)
        {
            // Spawn the tree at this GameObject's exact position and rotation
            treeInstance = Instantiate(treePrefab, transform.position, transform.rotation);
            // Make the spawned tree a child of this object to keep the hierarchy clean
            treeInstance.transform.SetParent(this.transform);
            
            // Grab the renderers from the newly spawned scene object
            targetRenderers = treeInstance.GetComponentsInChildren<Renderer>();
        }
        else
        {
            // Fallback: If no prefab is assigned, animate whatever renderers are already on this GameObject
            targetRenderers = GetComponentsInChildren<Renderer>();
        }

        // 2. Setup shader properties
        growthPropertyID = Shader.PropertyToID(growthPropertyName);
        propBlock = new MaterialPropertyBlock();

        // 3. Initialize Growth State
        if (setGrowAtStartTo0)
        {
            SetGrowthValue(0f);
        }

        if (startAtGameStart)
        {
            StartGrow();
        }
    }

    /// <summary>
    /// Can be fired from other scripts to initiate the growth animation over time.
    /// </summary>
    public void StartGrow()
    {
        StopAllCoroutines();
        StartCoroutine(GrowRoutine());
    }

    /// <summary>
    /// Instantly sets the growth phase to a specific value (0.0 to 1.0) on all renderers.
    /// </summary>
    public void SetGrowthValue(float value)
    {
        if (targetRenderers == null || targetRenderers.Length == 0) return;

        foreach (Renderer r in targetRenderers)
        {
            // Using MaterialPropertyBlock ensures we don't create duplicate material instances in memory
            r.GetPropertyBlock(propBlock);
            propBlock.SetFloat(growthPropertyID, value);
            r.SetPropertyBlock(propBlock);
        }
    }

    private IEnumerator GrowRoutine()
    {
        // Handle initial delay
        if (startDelayInSeconds > 0)
        {
            yield return new WaitForSeconds(startDelayInSeconds);
        }

        float elapsedTime = 0f;

        // Lerp the growth value over the specified time period
        while (elapsedTime < growthTimePeriod)
        {
            // Incorporate the speed multiplier
            elapsedTime += Time.deltaTime * speed;
            
            // Calculate the normalized linear phase (0 to 1)
            float linearPhase = Mathf.Clamp01(elapsedTime / growthTimePeriod);
            
            // Apply an S-curve (Ease-In-Out) to smooth the start and end
            float sCurvePhase = Mathf.SmoothStep(0f, 1f, linearPhase);
            
            SetGrowthValue(sCurvePhase);
            
            yield return null; // Wait for the next frame
        }

        // Ensure it mathematically caps out exactly at 1 at the end
        SetGrowthValue(1f);
    }
}