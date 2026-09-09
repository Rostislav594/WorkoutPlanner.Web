package com.gymplanner.wearos.ui

import org.junit.Assert.assertEquals
import org.junit.Test

class MinimalMvpViewModelTest {
    @Test
    fun remainingSeconds_roundsUpAndNeverBecomesNegative() {
        assertEquals(10, MinimalMvpViewModel.remainingSeconds(10_000L))
        assertEquals(10, MinimalMvpViewModel.remainingSeconds(9_001L))
        assertEquals(1, MinimalMvpViewModel.remainingSeconds(1L))
        assertEquals(0, MinimalMvpViewModel.remainingSeconds(0L))
        assertEquals(0, MinimalMvpViewModel.remainingSeconds(-1L))
    }
}
