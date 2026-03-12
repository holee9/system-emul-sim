# FPGA/CSI-2 처리

## CSI-2 프로토콜

CSI-2(Camera Serial Interface 2)는 MIPI Alliance의 표준 이미지 센서 인터페이스입니다.

### 파라미터

| 파라미터 | 범위 | 기본값 | 설명 |
|----------|------|--------|------|
| Lanes | 1–4 | 4 | CSI-2 데이터 레인 수 |
| DataRate | 0.1–4.5 Gbps | 2.5 Gbps | 레인당 데이터 레이트 |
| LineBuffer | 1–16 | 4 | 라인 버퍼 깊이 |

## 프레임 버퍼

MCU로 전송 전 프레임 데이터를 버퍼링합니다.

- **FrameBufferCount**: 2–16 프레임 (기본값 4)
- 오버플로우 감지 및 처리
- 동시성 제어 (생산자-소비자 패턴)
