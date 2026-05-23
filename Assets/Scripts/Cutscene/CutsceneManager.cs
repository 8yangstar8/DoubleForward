using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CutsceneManager : MonoBehaviour
{
    public static CutsceneManager Instance { get; private set; }

    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private float fadeDuration = 0.5f;

    public bool IsCutscenePlaying { get; private set; }

    public event System.Action OnCutsceneStart;
    public event System.Action OnCutsceneEnd;

    [System.Serializable]
    public class CutsceneAction
    {
        public enum ActionType
        {
            Dialogue,
            CameraMove,
            Wait,
            FadeIn,
            FadeOut,
            PlaySound,
            SpawnObject,
            DestroyObject,
            MoveObject,
            SetActive
        }

        public ActionType type;
        public float duration = 1f;

        // Dialogue
        public DialogueSystem.DialogueSequence dialogue;

        // Camera
        public Transform cameraTarget;
        public float cameraSize = 6f;

        // Sound
        public AudioClip audioClip;

        // Object manipulation
        public GameObject targetObject;
        public Vector3 targetPosition;
        public bool activeState;
    }

    void Awake()
    {
        Instance = this;
    }

    public void PlayCutscene(List<CutsceneAction> actions)
    {
        if (IsCutscenePlaying) return;
        StartCoroutine(ExecuteCutscene(actions));
    }

    private IEnumerator ExecuteCutscene(List<CutsceneAction> actions)
    {
        IsCutscenePlaying = true;
        OnCutsceneStart?.Invoke();

        // 禁用玩家输入
        var input = InputManager.Instance;

        foreach (var action in actions)
        {
            switch (action.type)
            {
                case CutsceneAction.ActionType.Dialogue:
                    if (action.dialogue != null && DialogueSystem.Instance != null)
                    {
                        DialogueSystem.Instance.StartDialogue(action.dialogue);
                        yield return new WaitUntil(() => !DialogueSystem.Instance.IsDialogueActive);
                    }
                    break;

                case CutsceneAction.ActionType.CameraMove:
                    yield return SmoothCameraMove(action.cameraTarget, action.cameraSize, action.duration);
                    break;

                case CutsceneAction.ActionType.Wait:
                    yield return new WaitForSeconds(action.duration);
                    break;

                case CutsceneAction.ActionType.FadeIn:
                    yield return Fade(0f, 1f, action.duration);
                    break;

                case CutsceneAction.ActionType.FadeOut:
                    yield return Fade(1f, 0f, action.duration);
                    break;

                case CutsceneAction.ActionType.PlaySound:
                    if (action.audioClip != null)
                        AudioManager.Instance?.PlaySFX(action.audioClip);
                    break;

                case CutsceneAction.ActionType.SpawnObject:
                    if (action.targetObject != null)
                        Instantiate(action.targetObject, action.targetPosition, Quaternion.identity);
                    break;

                case CutsceneAction.ActionType.DestroyObject:
                    if (action.targetObject != null)
                        Destroy(action.targetObject);
                    break;

                case CutsceneAction.ActionType.MoveObject:
                    if (action.targetObject != null)
                        yield return SmoothMove(action.targetObject.transform, action.targetPosition, action.duration);
                    break;

                case CutsceneAction.ActionType.SetActive:
                    if (action.targetObject != null)
                        action.targetObject.SetActive(action.activeState);
                    break;
            }
        }

        IsCutscenePlaying = false;
        OnCutsceneEnd?.Invoke();
    }

    private IEnumerator SmoothCameraMove(Transform target, float size, float duration)
    {
        var cam = Camera.main;
        if (cam == null || target == null) yield break;

        Vector3 startPos = cam.transform.position;
        float startSize = cam.orthographicSize;
        Vector3 endPos = new Vector3(target.position.x, target.position.y, startPos.z);

        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = Mathf.SmoothStep(0, 1, t / duration);
            cam.transform.position = Vector3.Lerp(startPos, endPos, progress);
            cam.orthographicSize = Mathf.Lerp(startSize, size, progress);
            yield return null;
        }
    }

    private IEnumerator SmoothMove(Transform obj, Vector3 endPos, float duration)
    {
        Vector3 startPos = obj.position;
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            obj.position = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0, 1, t / duration));
            yield return null;
        }
        obj.position = endPos;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fadeOverlay == null) yield break;
        fadeOverlay.gameObject.SetActive(true);

        float t = 0;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fadeOverlay.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        fadeOverlay.alpha = to;

        if (to <= 0) fadeOverlay.gameObject.SetActive(false);
    }

    public IEnumerator FadeToBlack(float duration = 0.5f)
    {
        yield return Fade(0, 1, duration);
    }

    public IEnumerator FadeFromBlack(float duration = 0.5f)
    {
        yield return Fade(1, 0, duration);
    }
}
