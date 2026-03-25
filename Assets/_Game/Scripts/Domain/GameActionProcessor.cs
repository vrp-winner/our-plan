using UnityEngine;

namespace Domain
{
    /// <summary>
    /// Domain Layer: จัดการ Logic หลักของเกม (Pure Logic) 
    /// ไม่มีหน้าที่รับ/ส่ง Network แต่ทำหน้าที่ประมวลผลข้อมูลเท่านั้น
    /// </summary>
    public static class GameActionProcessor
    {
        /// <summary>
        /// คำนวนการใช้ Points ของผู้เล่น
        /// </summary>
        /// <param name="currentPoints">Points ปัจจุบันของผู้เล่น</param>
        /// <param name="requestedAmount">Points ที่ต้องการใช้</param>
        /// <param name="isSick">สถานะป่วยหรือไม่</param>
        /// <param name="sickPenalty">ค่า penalty ของ Points หากป่วย</param>
        /// <param name="remainingPoints">Points ที่เหลือหลังจากการหัก (Output)</param>
        /// <returns>คืนค่า True หากมี Points พอและหักสำเร็จ, คืนค่า False หาก Points ไม่พอ</returns>
        public static bool TryProcessPointUsage(int currentPoints, int requestedAmount, bool isSick, int sickPenalty, out int remainingPoints)
        {
            // STEP 1: คืนค่าเริ่มต้นให้ Output
            remainingPoints = currentPoints;

            // STEP 2: ตรวจสอบว่า Points ตั้งต้นมีพอหรือไม่
            if (currentPoints < requestedAmount) 
            {
                return false;
            }

            // STEP 3: คำนวณ Points ที่ต้องใช้จริง (รวม Penalty หากมี)
            int finalCost = requestedAmount;
            if (isSick) 
            {
                finalCost += sickPenalty;
            }

            // STEP 4: หัก Points โดยไม่ให้ติดลบ
            remainingPoints = Mathf.Max(0, currentPoints - finalCost);
            
            return true;
        }

        /// <summary>
        /// ตรวจสอบและเตรียมข้อมูลก่อนเริ่ม movement
        /// </summary>
        /// <param name="currentPosition">ตำแหน่งปัจจุบันของตัวละคร</param>
        /// <param name="targetPosition">ตำแหน่งเป้าหมาย</param>
        /// <param name="validatedTarget">ตำแหน่งเป้าหมายที่ผ่านการ validate (output)</param>
        /// <returns>คืนค่า true หากสามารถเริ่ม movement ได้</returns>
        public static bool ValidateMovementSetup(Vector3 currentPosition, Vector3 targetPosition, out Vector3 validatedTarget)
        {
            // STEP 1: ล็อกแกน Y (Y-axis) ให้อยู่ระดับเดียวกัน ป้องกันตัวละครจมหรือลอย
            validatedTarget = new Vector3(targetPosition.x, currentPosition.y, targetPosition.z);

            // STEP 2: ตรวจสอบระยะห่าง (ถ้าอยู่ใกล้มากแล้ว ไม่จำเป็นต้องเคลื่อนที่)
            if (Vector3.Distance(currentPosition, validatedTarget) < 0.1f)
            {
                return false;
            }

            return true;
        }
    }
}