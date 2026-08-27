using UnityEngine;
using System.Collections;
using VLB;

public class IntroSceneGameController : MonoBehaviour
{
    [SerializeField] private Light spotlight;
    [SerializeField] private VolumetricDustParticles dustParticles;
    [SerializeField] private float startDelay = 0f;
    [SerializeField] private float fadeInDuration = 2f;
    [SerializeField] private float holdDuration = 0f;
    [SerializeField] private float fadeOutDuration = 2f;
    [SerializeField] private GameObject ObjOneTitle;
    [SerializeField] private GameObject ObjTwoGirlPart;
     [SerializeField] private GameObject ObjThreeWalllPart;
     [SerializeField] private GameObject ObjScene1Startpoint;
    [SerializeField] private Light spotlight2Girl;
    [SerializeField] private float secondLightStartDelay = 0f;
    [SerializeField] private float holdDuration2Girl = 0f;
    [SerializeField] private Light bodyFaintLight;
    [SerializeField] private Light windowShadowLight;
    [SerializeField] private float windowShadowLightDelay = 0f;
    [SerializeField] private float windowShadowLightHoldDuration = 0f;
    [SerializeField] private float windowShadowLightFadeOutDuration = 2f;

    [SerializeField] private float wallMoveDuration = 0f;
    [SerializeField] private AnimationCurve wallMoveSpeedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField] private GameObject wallOne;
    [SerializeField] private float wallOneMoveSpeed = 1f;
    [SerializeField] private bool wallOneReverseDirection = false;

    [SerializeField] private GameObject wallTwo;
    [SerializeField] private float wallTwoMoveSpeed = 1f;
    [SerializeField] private bool wallTwoReverseDirection = false;

    [SerializeField] private GameObject wallThree;
    [SerializeField] private float wallThreeMoveSpeed = 1f;
    [SerializeField] private bool wallThreeReverseDirection = false;

    [SerializeField] private GameObject wallFour;
    [SerializeField] private float wallFourMoveSpeed = 1f;
    [SerializeField] private bool wallFourReverseDirection = false;


    private float targetIntensity;
    private float targetDustAlpha;
    private float targetIntensity2Girl;
    private float targetBodyFaintLightIntensity;
    private float targetWindowShadowLightIntensity;
    private bool wallsMoving = false;
    private float wallMoveTimer = 0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ObjThreeWalllPart.SetActive(true);
        ObjScene1Startpoint.SetActive(false);
        ObjOneTitle.SetActive(true);
        
        targetIntensity = spotlight.intensity;
        spotlight.intensity = 0f;

        targetDustAlpha = dustParticles.alphaAdditionalRuntime;
        dustParticles.alphaAdditionalRuntime = 0f;

        targetIntensity2Girl = spotlight2Girl.intensity;
        spotlight2Girl.intensity = 0f;
        ObjTwoGirlPart.SetActive(false);

        targetBodyFaintLightIntensity = bodyFaintLight.intensity;
        bodyFaintLight.intensity = 0f;

        targetWindowShadowLightIntensity = windowShadowLight.intensity;
        windowShadowLight.intensity = 0f;

        StartCoroutine(RunLightSequence());
    }

    IEnumerator RunLightSequence()
    {
        yield return new WaitForSeconds(startDelay);
        yield return FadeLightAndDust(0f, targetIntensity, 0f, targetDustAlpha, fadeInDuration);

        yield return new WaitForSeconds(holdDuration);
        yield return FadeLightAndDust(spotlight.intensity, 0f, dustParticles.alphaAdditionalRuntime, 0f, fadeOutDuration);

        ObjOneTitle.SetActive(false);

        yield return new WaitForSeconds(secondLightStartDelay);
        ObjTwoGirlPart.SetActive(true);

        yield return FadeLightIntensity(spotlight2Girl, 0f, targetIntensity2Girl, fadeInDuration);
        yield return new WaitForSeconds(holdDuration2Girl);
        yield return FadeLightIntensity(spotlight2Girl, spotlight2Girl.intensity, 0f, fadeOutDuration);

        yield return FadeLightIntensity(bodyFaintLight, 0f, targetBodyFaintLightIntensity, fadeInDuration);

        ObjTwoGirlPart.SetActive(false);

        yield return new WaitForSeconds(windowShadowLightDelay);
        yield return FadeLightIntensity(windowShadowLight, 0f, targetWindowShadowLightIntensity, fadeInDuration);
        yield return new WaitForSeconds(windowShadowLightHoldDuration);

        wallMoveTimer = 0f;
        wallsMoving = true;
        yield return new WaitForSeconds(wallMoveDuration);
        wallsMoving = false;

        yield return FadeLightIntensity(windowShadowLight, windowShadowLight.intensity, 0f, windowShadowLightFadeOutDuration);

        ObjThreeWalllPart.SetActive(false);
        ObjScene1Startpoint.SetActive(true);
    }

    private void MoveWall(GameObject wall, float moveSpeed, bool reverseDirection, float speedMultiplier)
    {
        if (wall == null) return;

        float direction = reverseDirection ? 1f : -1f;
        wall.transform.Translate(direction * moveSpeed * speedMultiplier * Time.deltaTime * Vector3.up, Space.Self);
    }

    IEnumerator FadeLightIntensity(Light light, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            light.intensity = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        light.intensity = to;
    }

    IEnumerator FadeLightAndDust(float intensityFrom, float intensityTo, float dustAlphaFrom, float dustAlphaTo, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            spotlight.intensity = Mathf.Lerp(intensityFrom, intensityTo, t);
            dustParticles.alphaAdditionalRuntime = Mathf.Lerp(dustAlphaFrom, dustAlphaTo, t);
            yield return null;
        }
        spotlight.intensity = intensityTo;
        dustParticles.alphaAdditionalRuntime = dustAlphaTo;
    }

    // Update is called once per frame
    void Update()
    {
        if (!wallsMoving) return;

        wallMoveTimer += Time.deltaTime;
        float t = wallMoveDuration > 0f ? Mathf.Clamp01(wallMoveTimer / wallMoveDuration) : 1f;
        float speedMultiplier = wallMoveSpeedCurve.Evaluate(t);

        MoveWall(wallOne, wallOneMoveSpeed, wallOneReverseDirection, speedMultiplier);
        MoveWall(wallTwo, wallTwoMoveSpeed, wallTwoReverseDirection, speedMultiplier);
        MoveWall(wallThree, wallThreeMoveSpeed, wallThreeReverseDirection, speedMultiplier);
        MoveWall(wallFour, wallFourMoveSpeed, wallFourReverseDirection, speedMultiplier);
    }
}
