using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TempSubmitTest : MonoBehaviour
{
    public void OnClickDummySubmit()
    {
        if(PlayerState.Local == null)
        {
            Debug.LogWarning("[테스트] 로컬 플레이어를 찾을 수 없습니다.");
            return;
        }

        int[] dummyIndexes = new int[] { 0, 1 };

        Debug.Log("[테스트] 서버로 0, 1번 인덱스 카드 제출 요청");

        PlayerState.Local.SubmitCards(dummyIndexes);
    }
}
