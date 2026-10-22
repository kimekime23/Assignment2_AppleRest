Apple Rest - Storybook UI Kit (pencil + torn paper)
---------------------------------------------------
Unity 설정: 모든 PNG -> Texture Type = Sprite (2D and UI)
종이 조각은 늘리면 찢어진 가장자리가 뭉개지므로 9-slice 를 쓰지 않습니다.
Image 컴포넌트에서 Set Native Size 후, 비율 유지(Preserve Aspect)로 크기만 조절하세요.

종이 조각 (권장 용도)
- UI_Paper_Counter (340x120)  : 사과 카운터
- UI_Paper_Timer   (300x120)  : 타이머
- UI_Paper_Prompt  (760x110)  : 밑둥 안내
- UI_Paper_Result  (900x560)  : CLEAR / 실패 창 (연한 줄이 그어진 공책 종이)
- UI_Paper_Button / _Pressed (300x100) : 다시 하기 버튼

아이콘 (256x256, 수채화 + 연필선)
- UI_Icon_Apple, UI_Icon_Sun, UI_Icon_Stump, UI_Icon_Thorn

글꼴: NanumPenScript-Regular.ttf (나눔손글씨 펜, SIL OFL 1.1 - 게임에 무료 사용·포함 가능, OFL.txt 함께 보관)
- Project 창에서 ttf 우클릭 > Create > TextMeshPro > Font Asset (Dynamic)
- Dynamic 폰트 에셋은 필요한 한글을 자동으로 추가하므로 빌드에서도 깨지지 않습니다.
  (ttf 파일을 프로젝트에서 지우지 마세요)

글자 색
- 기본 글자: #423024 (연필 갈색)
- CLEAR 제목: #406E3E (초록 잉크) / 실패 제목: #96463C (붉은 잉크)
- "-3초!": #BA3A30
