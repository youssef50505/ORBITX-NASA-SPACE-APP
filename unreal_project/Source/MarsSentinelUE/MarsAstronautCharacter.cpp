#include "MarsAstronautCharacter.h"
#include "GameFramework/CharacterMovementComponent.h"

AMarsAstronautCharacter::AMarsAstronautCharacter()
{
    PrimaryActorTick.bCanEverTick = true;

    if (UCharacterMovementComponent* Move = GetCharacterMovement())
    {
        // Authentic Mars 0.379g gravity scale when World DefaultGravityZ = -980 or 1.0 when -372.076
        Move->GravityScale = 1.0f;
        Move->JumpZVelocity = 440.0f; // 4.4 m/s low-G Martian hop
        Move->MaxWalkSpeed = 420.0f;  // 4.2 m/s EVA pace
        Move->AirControl = 0.42f;
        Move->SetWalkableFloorAngle(48.0f);
    }
}

void AMarsAstronautCharacter::BeginPlay()
{
    Super::BeginPlay();
}

void AMarsAstronautCharacter::Tick(float DeltaSeconds)
{
    Super::Tick(DeltaSeconds);

    if (Telemetry.bIsInsideShelter)
    {
        Telemetry.OxygenPercent = FMath::Min(100.0f, Telemetry.OxygenPercent + 18.0f * DeltaSeconds);
        Telemetry.PowerPercent = FMath::Min(100.0f, Telemetry.PowerPercent + 16.0f * DeltaSeconds);
        Telemetry.RadiationTraumaPercent = FMath::Max(0.0f, Telemetry.RadiationTraumaPercent - 10.0f * DeltaSeconds);
    }
    else
    {
        const float StormMult = Telemetry.DustOpticalDepthTau > 1.2f ? 1.35f : 1.0f;
        Telemetry.OxygenPercent = FMath::Max(0.0f, Telemetry.OxygenPercent - 0.35f * StormMult * DeltaSeconds);
        Telemetry.PowerPercent = FMath::Max(0.0f, Telemetry.PowerPercent - 0.18f * DeltaSeconds);
        const float RadRate = Telemetry.DustOpticalDepthTau > 1.2f ? (Telemetry.DustOpticalDepthTau - 1.0f) * 1.45f : 0.06f;
        Telemetry.RadiationTraumaPercent = FMath::Clamp(Telemetry.RadiationTraumaPercent + RadRate * DeltaSeconds, 0.0f, 100.0f);
        Telemetry.CumulativeRadMsv += (0.002f + RadRate * 0.006f) * DeltaSeconds;
    }
}

void AMarsAstronautCharacter::AdvanceStoryChapter()
{
    const uint8 NextVal = FMath::Min<uint8>(static_cast<uint8>(EMarsStoryChapter::Epilogue_Complete), static_cast<uint8>(CurrentChapter) + 1);
    CurrentChapter = static_cast<EMarsStoryChapter>(NextVal);

    if (CurrentChapter == EMarsStoryChapter::Ch3_SolarFlareTriage)
    {
        SetDustOpticalDepth(2.35f);
    }
    else if (CurrentChapter == EMarsStoryChapter::Ch4_BelvaRelayAndMeteorite)
    {
        SetDustOpticalDepth(0.45f);
    }
}

void AMarsAstronautCharacter::TriggerRimfaxXRayScan()
{
    // Triggers Niagara subsurface pulse ring & Material Parameter Collection scan wave in BP/Level
    Telemetry.PowerPercent = FMath::Max(0.0f, Telemetry.PowerPercent - 1.2f);
}

void AMarsAstronautCharacter::SetDustOpticalDepth(float NewTau)
{
    Telemetry.DustOpticalDepthTau = FMath::Clamp(NewTau, 0.35f, 3.20f);
}

float AMarsAstronautCharacter::ComputeBeerLambertTransmission(float Tau, float SolarElevationDeg)
{
    const float ClampedDeg = FMath::Clamp(SolarElevationDeg, 5.0f, 90.0f);
    const float AirMass = 1.0f / FMath::Max(0.087f, FMath::Sin(FMath::DegreesToRadians(ClampedDeg)));
    return FMath::Exp(-FMath::Max(0.0f, Tau) * AirMass);
}

FText AMarsAstronautCharacter::GetActiveChapterDialogue() const
{
    switch (CurrentChapter)
    {
    case EMarsStoryChapter::Ch1_DustfallArrival:
        return FText::FromString(bArabicLanguage
            ? TEXT("أوربت إكس-1، أهلاً بك في فوهة جيزيرو — تأخير الاتصال 18 دقيقة. افحص المسكن واركب بيرسيفيرانس.")
            : TEXT("ORBITX-1, welcome to Octavia E. Butler Base. 18m 42s comms delay — inspect NASA HDU and board Perseverance."));
    case EMarsStoryChapter::Ch2_CheyavaBiosignature:
        return FText::FromString(bArabicLanguage
            ? TEXT("مسبار RIMFAX رصد طبقة عضوية تحت شيفا فولز! فعّل الماسح [X] واجمع العينة #1.")
            : TEXT("RIMFAX radar pinged organic mudstone at Cheyava Falls! Trigger [X] X-Ray Scanner and secure Core #1."));
    case EMarsStoryChapter::Ch3_SolarFlareTriage:
        return FText::FromString(bArabicLanguage
            ? TEXT("إنذار DONKI! عاصفة غبارية Tau 2.4 وتوهج شمسي — احتمِ فوراً داخل الوحدة الطبية أو HDU!")
            : TEXT("DONKI SEP ALERT! Tau 2.4 Dust Storm striking Jezero — shelter immediately inside Medical Bay or HDU!"));
    case EMarsStoryChapter::Ch4_BelvaRelayAndMeteorite:
        return FText::FromString(bArabicLanguage
            ? TEXT("انقشعت العاصفة مع غروب المريخ الأزرق! مسار A* الآمن (8.1°) مفتوح نحو نيزك كاكاو وحافة بيلفا.")
            : TEXT("Storm clearing into Martian blue sunset! A* Safest Route (8.1 deg) open to Cacao Meteorite and Belva Rim."));
    case EMarsStoryChapter::Ch5_SampleReturnIgnition:
        return FText::FromString(bArabicLanguage
            ? TEXT("جميع العينات الثلاث مؤمّنة! بدء العد التنازلي لإقلاع صاروخ العودة إلى الأرض... إقلاع!")
            : TEXT("All 3 Jezero cores sealed aboard Mars Ascent Vehicle! Main engine ignition — T-3, 2, 1, LIFTOFF!"));
    default:
        return FText::FromString(bArabicLanguage
            ? TEXT("اكتملت حملة سول 1042 بنجاح — عينات جيزيرو في طريقها للأرض!")
            : TEXT("Sol 1042 Campaign Complete — Jezero cores bound for Earth return!"));
    }
}
