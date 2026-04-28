using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

namespace Systems.UI
{
    /// <summary>
    /// ตัวจัดการ UI เมนูหลักระหว่างเล่นเกม (Local UI - ไม่ Sync ผ่าน Network)
    /// </summary>
    public class GameMenuUIManager : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("ปุ่มครึ่งวงกลม ด้านบนหน้าจอ")]
        public Button menuButton;
        
        [Tooltip("หน้าต่างเมนูทั้งหมด (พื้นหลัง + ปุ่มข้างใน)")]
        public GameObject menuPanel;
        
        [Tooltip("ปุ่มกลับหน้าแรก")]
        public Button exitToMenuButton;
        
        [Tooltip("ปุ่มเล่นต่อ")]
        public Button continueButton;

        private void Start()
        {
            // เริ่มเกมมา ปิด Menu Panel ไว้ก่อน
            if (menuPanel != null) menuPanel.SetActive(false);

            // ผูกคำสั่งกับปุ่มต่างๆ
            if (menuButton != null) menuButton.onClick.AddListener(ToggleMenu);
            if (continueButton != null) continueButton.onClick.AddListener(CloseMenu);
            if (exitToMenuButton != null) exitToMenuButton.onClick.AddListener(HandleExitToMenu);
            
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void OnDestroy()
        {
            // ล้างคำสั่งออกจากปุ่มเมื่อ UI ถูกทำลาย ป้องกัน Memory Leak
            if (menuButton != null) menuButton.onClick.RemoveAllListeners();
            if (continueButton != null) continueButton.onClick.RemoveAllListeners();
            if (exitToMenuButton != null) exitToMenuButton.onClick.RemoveAllListeners();
            
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        /// <summary>
        /// เปิด / ปิดหน้าต่างเมนู
        /// </summary>
        public void ToggleMenu()
        {
            if (menuPanel != null) menuPanel.SetActive(!menuPanel.activeSelf);
        }

        /// <summary>
        /// ปิดหน้าต่างเมนู (กลับไปเล่นต่อ)
        /// </summary>
        public void CloseMenu()
        {
            if (menuPanel != null) menuPanel.SetActive(false);
        }

        /// <summary>
        /// จัดการเมื่อกดปุ่มออกเกม
        /// </summary>
        private void HandleExitToMenu()
        {
            // ป้องกันปุ่มถูกกดซ้ำ
            exitToMenuButton.interactable = false;
            Debug.Log("[GameMenu] กำลังออกจากเกม...");

            // ใช้ Coroutine เพื่อให้เวลา NetworkManager ทำการ Shutdown ตัวเองอย่างสมบูรณ์
            StartCoroutine(ShutdownAndLeaveCoroutine());
        }
        
        private IEnumerator ShutdownAndLeaveCoroutine()
        {
            // 1. ปิดระบบ Network
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
                // รอจนกว่าระบบ Shutdown จะปิดตัวเองเสร็จจริงๆ
                yield return new WaitWhile(() => NetworkManager.Singleton != null && NetworkManager.Singleton.ShutdownInProgress);
                
                // ทำลาย NetworkManager ทิ้งไป เพื่อสร้างใหม่ใน MainMenu
                if (NetworkManager.Singleton != null)
                {
                    Destroy(NetworkManager.Singleton.gameObject);
                }
            }
            
            // ล้างไพ่ Manager อื่นๆ
            DestroyObjectByName("GameSystem");
            DestroyObjectByName("LobbyNetworkManager");

            // รออีก 1 เฟรมให้ชัวร์ว่าขยะโดนเคลียร์
            yield return null; 

            // โหลดกลับหน้าแรก
            SceneManager.LoadScene("MainMenu");
        }
        
        // ทำงานอัตโนมัติถ้า Host ปิดห้อง หรือเน็ตหลุด
        private void OnClientDisconnected(ulong clientId)
        {
            // เช็คว่าถ้าตัวเองหลุด หรือ Server ปิด ให้กลับหน้าแรก
            if (clientId == NetworkManager.Singleton.LocalClientId || clientId == NetworkManager.ServerClientId)
            {
                Debug.Log("[GameMenu] ขาดการเชื่อมต่อ โหลดกลับหน้าแรก...");
                StartCoroutine(ShutdownAndLeaveCoroutine());
            }
        }

        /// <summary>
        /// ฟังก์ชันสำหรับค้นหาและทำลาย GameObject ข้าม Scene
        /// </summary>
        private void DestroyObjectByName(string objectName)
        {
            GameObject obj = GameObject.Find(objectName);
            if (obj != null)
            {
                Destroy(obj);
                Debug.Log($"[GameMenu] ทำลาย {objectName} ก่อนกลับหน้าหลัก");
            }
        }
    }
}