/**
 * @file mock_yaml.h
 * @brief Mock YAML functions for testing
 */

#ifndef MOCK_YAML_H
#define MOCK_YAML_H

#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

/**
 * @brief Set mock YAML content for testing
 *
 * @param content YAML content to use (NULL for file not found simulation)
 *
 * @note This function overrides the file system for YAML loading.
 *       Tests can set custom YAML content or NULL to simulate errors.
 */
void mock_yaml_set_content(const char *content);

/**
 * @brief Get mock YAML content
 *
 * @return Current mock YAML content, or default if not set
 */
const char *mock_yaml_get_content(void);

/**
 * @brief Reset mock YAML content to default
 */
void mock_yaml_reset_content(void);

#ifdef __cplusplus
}
#endif

#endif /* MOCK_YAML_H */
