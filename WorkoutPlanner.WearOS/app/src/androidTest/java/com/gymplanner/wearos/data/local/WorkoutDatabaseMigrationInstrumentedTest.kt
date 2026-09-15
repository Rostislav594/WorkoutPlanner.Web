package com.gymplanner.wearos.data.local

import androidx.sqlite.db.SupportSQLiteDatabase
import androidx.sqlite.db.SupportSQLiteOpenHelper
import androidx.sqlite.db.framework.FrameworkSQLiteOpenHelperFactory
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class WorkoutDatabaseMigrationInstrumentedTest {
    private val context = InstrumentationRegistry.getInstrumentation().targetContext
    private val databaseName = "stage14-migration-test.db"

    @After
    fun cleanUp() {
        context.deleteDatabase(databaseName)
    }

    @Test
    fun migration1To2_preservesWorkoutAndAddsEmptyName() {
        createVersionOneDatabase().use { helper ->
            helper.writableDatabase.execSQL(
                """
                INSERT INTO local_workouts (
                    workoutId, isActive, lastCheckedAtUtcMillis,
                    restCompletedSetId, restEndsAtUtcMillis, restDurationSeconds
                ) VALUES (17, 1, 1234, NULL, NULL, NULL)
                """.trimIndent(),
            )
        }

        createVersionTwoDatabase().use { helper ->
            helper.writableDatabase.query(
                "SELECT workoutId, workoutName, isActive FROM local_workouts WHERE workoutId = 17",
            ).use { cursor ->
                check(cursor.moveToFirst())
                assertEquals(17L, cursor.getLong(0))
                assertEquals("", cursor.getString(1))
                assertEquals(1, cursor.getInt(2))
            }
        }
    }

    private fun createVersionOneDatabase(): SupportSQLiteOpenHelper =
        FrameworkSQLiteOpenHelperFactory().create(
            SupportSQLiteOpenHelper.Configuration.builder(context)
                .name(databaseName)
                .callback(object : SupportSQLiteOpenHelper.Callback(1) {
                    override fun onCreate(db: SupportSQLiteDatabase) {
                        db.execSQL(
                            """
                            CREATE TABLE local_workouts (
                                workoutId INTEGER NOT NULL PRIMARY KEY,
                                isActive INTEGER NOT NULL,
                                lastCheckedAtUtcMillis INTEGER NOT NULL,
                                restCompletedSetId INTEGER,
                                restEndsAtUtcMillis INTEGER,
                                restDurationSeconds INTEGER
                            )
                            """.trimIndent(),
                        )
                    }

                    override fun onUpgrade(
                        db: SupportSQLiteDatabase,
                        oldVersion: Int,
                        newVersion: Int,
                    ) = Unit
                })
                .build(),
        )

    private fun createVersionTwoDatabase(): SupportSQLiteOpenHelper =
        FrameworkSQLiteOpenHelperFactory().create(
            SupportSQLiteOpenHelper.Configuration.builder(context)
                .name(databaseName)
                .callback(object : SupportSQLiteOpenHelper.Callback(2) {
                    override fun onCreate(db: SupportSQLiteDatabase) = Unit

                    override fun onUpgrade(
                        db: SupportSQLiteDatabase,
                        oldVersion: Int,
                        newVersion: Int,
                    ) {
                        check(oldVersion == 1 && newVersion == 2)
                        WorkoutDatabase.migration1To2.migrate(db)
                    }
                })
                .build(),
        )
}
