# Host 파이프라인

## 프레임 수신 메커니즘

Host 계층은 MCU UDP 패킷을 수신하고 완성된 프레임을 재조립합니다.

## 실시간 표시

- **Frame Preview 탭**: WriteableBitmap 기반 실시간 렌더링
- **프레임 레이트**: 최대 15 fps 표시 (시뮬레이션 속도와 독립)
- **Window/Level**: 조정 가능한 그레이스케일 매핑

## 처리 통계

Status Dashboard에서 확인 가능한 통계:
- 수신 프레임 수
- 드롭된 프레임 수
- 처리량 (Gbps)
- 연결 상태
