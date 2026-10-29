using UnityEngine;

/// <summary>
/// Solo para pruebas. Teclas 1-5 disparan cada tipo de evento de la IA.
/// Quitar de la escena antes del build final.
/// </summary>
public class AICompanionTester : MonoBehaviour
{
    private void Update()
    {
        var ai = AICompanion.Instance;
        if (ai == null) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) ai.Speak(AITrigger.ClueFound, "Nota de Porky: escrita en tono cómplice, dirigida a Don Lobo");
        if (Input.GetKeyDown(KeyCode.Alpha2)) ai.Speak(AITrigger.InterrogationEnded, "el guardia dio información útil");
        if (Input.GetKeyDown(KeyCode.Alpha3)) ai.Speak(AITrigger.EnemyKilled);
        if (Input.GetKeyDown(KeyCode.Alpha4)) ai.Speak(AITrigger.FinalConfrontation);
        if (Input.GetKeyDown(KeyCode.Alpha5)) ai.Speak(AITrigger.LowHealth);
    }
}
