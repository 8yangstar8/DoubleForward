using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Image speakerPortrait;
    [SerializeField] private Image backgroundOverlay;
    [SerializeField] private GameObject continueIndicator;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Settings")]
    [SerializeField] private float typewriterSpeed = 0.03f;
    [SerializeField] private float autoAdvanceDelay = 2f;
    [SerializeField] private bool autoAdvance;

    [System.Serializable]
    public class DialogueLine
    {
        public string speakerName;
        public Sprite portrait;
        [TextArea(2, 5)] public string text;
        public Color nameColor = Color.white;
        public AudioClip voiceClip;
        public float delay; // 在这句之前等待的时间
    }

    [System.Serializable]
    public class DialogueSequence
    {
        public string id;
        public List<DialogueLine> lines = new List<DialogueLine>();
        public bool pauseGameplay = true;
    }

    public bool IsDialogueActive { get; private set; }

    private Queue<DialogueLine> lineQueue = new Queue<DialogueLine>();
    private bool isTyping;
    private bool skipRequested;
    private Coroutine typewriterCoroutine;

    public event System.Action OnDialogueStart;
    public event System.Action OnDialogueEnd;

    void Awake()
    {
        Instance = this;
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    void Update()
    {
        if (!IsDialogueActive) return;

        // 点击屏幕推进对话
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) ||
            (InputManager.Instance != null && InputManager.Instance.GetJumpPressed(0)))
        {
            if (isTyping)
                skipRequested = true;
            else
                ShowNextLine();
        }
    }

    public void StartDialogue(DialogueSequence sequence)
    {
        if (sequence == null || sequence.lines.Count == 0) return;

        IsDialogueActive = true;
        lineQueue.Clear();

        foreach (var line in sequence.lines)
            lineQueue.Enqueue(line);

        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        if (backgroundOverlay != null) backgroundOverlay.gameObject.SetActive(sequence.pauseGameplay);

        if (sequence.pauseGameplay)
            Time.timeScale = 0f;

        OnDialogueStart?.Invoke();
        StartCoroutine(FadeInPanel());
        ShowNextLine();
    }

    public void StartDialogue(List<DialogueLine> lines, bool pauseGame = true)
    {
        var seq = new DialogueSequence { lines = lines, pauseGameplay = pauseGame };
        StartDialogue(seq);
    }

    private void ShowNextLine()
    {
        if (lineQueue.Count == 0)
        {
            EndDialogue();
            return;
        }

        var line = lineQueue.Dequeue();

        if (speakerNameText != null)
        {
            speakerNameText.text = line.speakerName;
            speakerNameText.color = line.nameColor;
        }

        if (speakerPortrait != null)
        {
            if (line.portrait != null)
            {
                speakerPortrait.sprite = line.portrait;
                speakerPortrait.gameObject.SetActive(true);
            }
            else
            {
                speakerPortrait.gameObject.SetActive(false);
            }
        }

        if (line.voiceClip != null)
            AudioManager.Instance?.PlaySFX(line.voiceClip);

        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);

        typewriterCoroutine = StartCoroutine(TypewriterEffect(line.text));
    }

    private IEnumerator TypewriterEffect(string fullText)
    {
        isTyping = true;
        skipRequested = false;
        dialogueText.text = "";

        foreach (char c in fullText)
        {
            if (skipRequested)
            {
                dialogueText.text = fullText;
                break;
            }

            dialogueText.text += c;

            if (c != ' ')
                yield return new WaitForSecondsRealtime(typewriterSpeed);
        }

        isTyping = false;

        if (continueIndicator != null)
            continueIndicator.SetActive(true);

        if (autoAdvance)
        {
            yield return new WaitForSecondsRealtime(autoAdvanceDelay);
            ShowNextLine();
        }
    }

    private void EndDialogue()
    {
        IsDialogueActive = false;
        Time.timeScale = 1f;

        StartCoroutine(FadeOutPanel());
        OnDialogueEnd?.Invoke();
    }

    private IEnumerator FadeInPanel()
    {
        if (canvasGroup == null) yield break;
        canvasGroup.alpha = 0;
        float t = 0;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = t / 0.3f;
            yield return null;
        }
        canvasGroup.alpha = 1;
    }

    private IEnumerator FadeOutPanel()
    {
        if (canvasGroup == null)
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            yield break;
        }

        float t = 0;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - (t / 0.3f);
            yield return null;
        }

        if (dialoguePanel != null) dialoguePanel.SetActive(false);
    }

    public void SkipAll()
    {
        StopAllCoroutines();
        EndDialogue();
    }
}
