# 파라미터 레퍼런스

## Panel 파라미터

| 이름 | 타입 | 범위 | 기본값 | 단위 | 설명 |
|------|------|------|--------|------|------|
| kVp | double | 40–150 | 80 | kV | X선관 가속 전압 |
| mAs | double | 0.1–500 | 10.0 | mAs | X선 방사선량 |
| Rows | int | 64–4096 | 128 | pixel | 프레임 세로 해상도 |
| Cols | int | 64–4096 | 128 | pixel | 프레임 가로 해상도 |
| NoiseType | enum | Gaussian/Poisson/None | Gaussian | - | 노이즈 모델 |
| DefectRate | double | 0.0–1.0 | 0.001 | ratio | 결함 픽셀 비율 |
| FrameRate | double | 0.1–30 | 10 | fps | 시뮬레이션 프레임 레이트 |

## CSI-2/FPGA 파라미터

| 이름 | 타입 | 범위 | 기본값 | 단위 | 설명 |
|------|------|------|--------|------|------|
| Lanes | int | 1–4 | 4 | - | CSI-2 데이터 레인 수 |
| DataRate | double | 0.1–4.5 | 2.5 | Gbps | 레인당 데이터 레이트 |
| LineBufferDepth | int | 1–16 | 4 | - | 라인 버퍼 깊이 |
| FrameBufferCount | int | 2–16 | 4 | - | 프레임 버퍼 수 |

## Network 파라미터

| 이름 | 타입 | 범위 | 기본값 | 단위 | 설명 |
|------|------|------|--------|------|------|
| PacketLossRate | double | 0.0–1.0 | 0.0 | ratio | UDP 패킷 손실률 |
| ReorderRate | double | 0.0–1.0 | 0.0 | ratio | 패킷 재정렬률 |
| CorruptionRate | double | 0.0–1.0 | 0.0 | ratio | 패킷 손상률 |
| UdpPort | int | 1024–65535 | 8000 | - | UDP 수신 포트 |
