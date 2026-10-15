using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TempSubmitTest : MonoBehaviour
{

    private List<int> selectedIndexes = new List<int>();

    public void Debug_OnClickDummySubmit()
    {
        if(PlayerState.Local == null)
        {
            Debug.LogWarning("[테스트] 로컬 플레이어를 찾을 수 없습니다.");
            return;
        }

        if (selectedIndexes.Count == 0)
        {
            Debug.LogWarning("[테스트] 선택된 카드가 없습니다.");
            return;
        }

        Debug.Log($"[테스트] 서버로 {selectedIndexes.Count}장 제출 요청.");

        InGameEvent.TriggerOnCardsPlayed(selectedIndexes.ToArray());
        selectedIndexes.Clear();
    }

    public void Debug_OnClickAddCard(int index)
    {
        if(!selectedIndexes.Contains(index))
        {
            selectedIndexes.Add(index);
            Debug.Log($"[임시 UI] : {index}번 인덱스 담김. 현재 {selectedIndexes.Count} 장 선택 중");
        }
    }

    public void Debug_ClearSelection()
    {
        selectedIndexes.Clear();
        Debug.Log("[임시 UI] : 선택 초기화됨");
    }

    public void Debug_CallLiar()
    {
        Debug.Log("[인게임] : 라이어 고발");
        PlayerState.Local.CallLiar();
    }

}
