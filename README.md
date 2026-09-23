# Addressable Dependency Analyzer

Addressables 빌드 레이아웃을 분석하고 로컬 에셋에서 원격 에셋으로 이어지는 참조를 찾는 Unity Editor 유틸리티입니다.

## 개요

이 도구는 Addressables 빌드 레이아웃을 검사하여 로컬 빌드 경로의 에셋이 원격 빌드 경로의 에셋을 참조하는 경우를 알려줍니다. Addressables 프로젝트에서 원격 의존성 관계를 파악하는 데 도움을 줍니다.

## 주요 기능

- Addressables 빌드 레이아웃 파일에서 `BuildLayout` 데이터를 파싱합니다.
- GUID를 기준으로 Addressables 그룹을 매핑하여 로컬 그룹과 원격 그룹을 구분합니다.
- 로컬 에셋에서 원격 에셋으로 연결되는 참조를 수집합니다.
- `RemoteDepGroups`를 통해 의존성 맵을 제공합니다.
- 로컬 에셋에서 원격 에셋으로 이어지는 참조를 Unity Debug 로그로 출력합니다.

## 요구 사항

- Unity 2018.1 이상
- `com.unity.addressables` 패키지 설치

## 설치

1. 이 저장소의 `Editor` 폴더를 Unity 프로젝트 루트에 복사합니다.
2. Addressables가 설치되고 설정되어 있는지 확인합니다.
3. 빌드 레이아웃 `.json` 또는 `.bin` 파일을 확인할 수 있는 경로에 둡니다.

## Unity Package Git 설치

Unity 프로젝트의 `Packages/manifest.json`에 다음과 같이 의존성을 추가하면 Git을 통해 이 도구를 설치할 수 있습니다.

```json
{
  "dependencies": {
    "com.unity.addressables": "2.9.1",
    "addressable-dependency-analyzer": "https://github.com/josangjun/addressable-dependency-analyzer.git"
  }
}
```

> `https://github.com/josangjun/addressable-dependency-analyzer.git` 부분은 실제 Git 저장소 URL로 바꾸세요.

브랜치 또는 커밋을 지정하려면 다음 형식을 사용하세요.

```json
{
  "dependencies": {
    "addressable-dependency-analyzer": "https://github.com/josangjun/addressable-dependency-analyzer.git#main"
  }
}
```

## 사용법

Editor 코드 또는 별도로 만든 메뉴/윈도우에서 `AddressablesBuildLayoutAnalyzer`를 사용합니다.

예시:

```csharp
var analyzer = new XSystem.Addressable.Analyzer.AddressablesBuildLayoutAnalyzer(buildLayoutPath);
analyzer.PrintLocalToRemoteRefs();
```

의존성 맵에 접근하려면 다음과 같이 작성합니다.

```csharp
var remoteDeps = analyzer.RemoteDepGroups;
```

## 프로젝트 구조

- `Editor/AddressablesBuildLayoutAnalyzer.cs` - 메인 분석기 구현
- `Editor/AddressablesDependencyWindow.cs` - 의존성 시각화를 위한 Editor 윈도우
- `Editor/AddressablesStaticRemoteDependencyReporter.cs` - 정적 보고서 생성 도우미
- `Editor/AddressGroup.cs` - Addressable 그룹 도우미

## 참고

이 저장소는 Editor 전용 유틸리티이므로 Unity 프로젝트의 `Editor` 폴더 아래에 배치해야 합니다.
