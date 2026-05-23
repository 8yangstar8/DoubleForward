using UnityEngine;

public class SplitScreenManager : MonoBehaviour
{
    public static SplitScreenManager Instance { get; private set; }

    [SerializeField] private Camera cameraP1;
    [SerializeField] private Camera cameraP2;
    [SerializeField] private CameraController controllerP1;
    [SerializeField] private CameraController controllerP2;

    public enum ScreenMode { SingleScreen, SplitHorizontal, SplitVertical }
    public ScreenMode CurrentMode { get; private set; } = ScreenMode.SingleScreen;

    void Awake()
    {
        Instance = this;
    }

    public void SetMode(ScreenMode mode)
    {
        CurrentMode = mode;

        switch (mode)
        {
            case ScreenMode.SingleScreen:
                SetSingleScreen();
                break;
            case ScreenMode.SplitHorizontal:
                SetSplitHorizontal();
                break;
            case ScreenMode.SplitVertical:
                SetSplitVertical();
                break;
        }
    }

    private void SetSingleScreen()
    {
        if (cameraP1 != null)
        {
            cameraP1.rect = new Rect(0, 0, 1, 1);
            cameraP1.gameObject.SetActive(true);
        }
        if (cameraP2 != null)
            cameraP2.gameObject.SetActive(false);
    }

    private void SetSplitHorizontal()
    {
        if (cameraP1 != null)
        {
            cameraP1.rect = new Rect(0, 0.5f, 1, 0.5f);
            cameraP1.gameObject.SetActive(true);
        }
        if (cameraP2 != null)
        {
            cameraP2.rect = new Rect(0, 0, 1, 0.5f);
            cameraP2.gameObject.SetActive(true);
        }
    }

    private void SetSplitVertical()
    {
        if (cameraP1 != null)
        {
            cameraP1.rect = new Rect(0, 0, 0.5f, 1);
            cameraP1.gameObject.SetActive(true);
        }
        if (cameraP2 != null)
        {
            cameraP2.rect = new Rect(0.5f, 0, 0.5f, 1);
            cameraP2.gameObject.SetActive(true);
        }
    }

    public void SetTargets(Transform targetP1, Transform targetP2)
    {
        if (controllerP1 != null) controllerP1.SetTarget(targetP1);
        if (controllerP2 != null) controllerP2.SetTarget(targetP2);
    }

    public void SnapCameras()
    {
        if (controllerP1 != null) controllerP1.SnapToTarget();
        if (controllerP2 != null) controllerP2.SnapToTarget();
    }
}
