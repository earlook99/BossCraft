namespace GameSystem
{
    public static class GameConstants
    {
        public static class Battle
        {
            public const int PLAYER_COUNT = 4;
            public const int BOSS_INDEX = 4;
            public const int TOTAL_ENTITIES = 5;
            
            public const float MESSAGE_DURATION = 1f;
            public const float EFFECT_DURATION = 2f;
            public const float MULTI_HIT_DELAY = 0.5f;
            
            public const int MIN_MULTI_HITS = 2;
            public const int MAX_MULTI_HITS = 5;
            
            public const float CRITICAL_MULTIPLIER = 1.5f;
            public const int MINIMUM_DAMAGE = 1;
            
            public const float MIN_STAT_MULTIPLIER = 0.6f;
            public const float MAX_STAT_MULTIPLIER = 1.4f;
            
            public const int SHIELD_BREAK_STUN_DURATION = 1;
            public const float SHIELD_DAMAGE_REDUCTION = 0.5f;
            
            public const float GUARD_DEFENSE_MULTIPLIER = 2f;
            public const int DEFENSE_FORMULA_BASE = 100;
        }
        
        public static class UI
        {
            public const float HP_ANIMATION_DURATION = 1f;
            public const float DEFAULT_MESSAGE_DURATION = 2f;
            
            public const float HP_FLASH_DURATION = 0.2f;
            public const float SHIELD_ANIM_DURATION = 0.3f;
            public const float SHIELD_INITIAL_SCALE = 1.3f;
            public const float SHIELD_ALPHA = 0.9f;
            
            public const float DIVIDER_WIDTH = 4f;
            public const float DIVIDER_FADE_TIME = 0.1f;
            public const float DIVIDER_DELAY = 0.05f;
            
            public const float ACTIVE_SPRITE_ALPHA = 1f;
            public const float INACTIVE_SPRITE_ALPHA = 0.1f;
        }
        
        public static class Camera
        {
            public const int DEFAULT_PRIORITY = 0;
            public const int ACTIVE_PRIORITY = 10;
        }
        
        public static class AI
        {
            public const float BEST_CHOICE_PROBABILITY = 0.65f;
            public const float SECOND_CHOICE_PROBABILITY = 0.20f;
            public const float SHIELD_PRIORITY_SCORE = 1000f;
            public const int INVALID_MOVE_INDEX = -1;
        }
        
        public static class Network
        {
            public const int REQUEST_TIMEOUT = 60;
            public const int GENERATION_TIMEOUT = 180;
            public const int JPEG_QUALITY = 85;
            public const float PROGRESS_UPDATE_INTERVAL = 0.5f;
            
            public const string STATUS_ENDPOINT = "/status";
            public const string PREDICT_ENDPOINT = "/api/predict";
            public const string DATA_IMAGE_PREFIX = "data:image/";
            public const char DATA_SEPARATOR = ',';
        }
        
        public static class Scene
        {
            public const string MAIN_MENU_SCENE = "MainMenu";
            public const string BATTLE_SCENE = "Battle";
            public const string BOSS_CREATION_SCENE = "BossCreation";
        }
    }
}