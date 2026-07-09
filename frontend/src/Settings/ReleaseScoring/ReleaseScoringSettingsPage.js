/* eslint-disable react/prop-types */
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Alert from 'Components/Alert';
import Card from 'Components/Card';
import FieldSet from 'Components/FieldSet';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import IconButton from 'Components/Link/IconButton';
import SpinnerButton from 'Components/Link/SpinnerButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { icons } from 'Helpers/Props';
import SettingsToolbar from 'Settings/SettingsToolbar';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
import styles from './ReleaseScoringSettingsPage.css';

const ENDPOINTS = {
  languageMapping: '/audiolanguagemapping',
  audioScoreProfile: '/audioscoreprofile',
  languagePreference: '/audiolanguagepreference'
};

const MATCH_TYPES = [
  { key: 'contains', value: 'Contains' },
  { key: 'normalizedContains', value: 'Normalized Contains' },
  { key: 'regex', value: 'Regex' }
];

function requestJson({ url, method = 'GET', data }) {
  const ajaxOptions = {
    url,
    method
  };

  if (method !== 'DELETE') {
    ajaxOptions.dataType = 'json';
  }

  if (data !== undefined) {
    ajaxOptions.contentType = 'application/json';
    ajaxOptions.data = JSON.stringify(data);
  }

  return new Promise((resolve, reject) => {
    const { request } = createAjaxRequest(ajaxOptions);

    request.done((result) => resolve(result));
    request.fail((xhr) => reject(xhr));
  });
}

function getErrorMessage(xhr, fallback) {
  const response = xhr?.responseJSON;

  if (Array.isArray(response)) {
    return response
      .map((item) => item.errorMessage || item.message || item.propertyName)
      .filter(Boolean)
      .join(' ');
  }

  if (response?.errors) {
    return Object.values(response.errors).flat().join(' ');
  }

  return response?.message || response?.error || xhr?.statusText || fallback;
}

function getLanguageName(language) {
  return language?.name || translate('Unknown');
}

function getDefaultLanguage(languages) {
  return (
    languages.find((language) => language.name === 'Chinese') ||
    languages.find((language) => language.id > 0) ||
    languages[0] || { id: 0, name: 'Unknown', nameLower: 'unknown' }
  );
}

function createDefaultLanguageMapping(languages) {
  const language = getDefaultLanguage(languages);
  const aliases = language.name === 'Chinese' ?
    ['Chinese', 'Mandarin', 'Guoyu'] :
    [language.name];

  return {
    id: 0,
    language,
    aliases,
    enabled: true
  };
}

function createDefaultAudioScoreProfile() {
  return {
    id: 0,
    name: translate('DefaultAudioScoreProfileName'),
    enabled: true,
    rules: [
      {
        name: 'Atmos',
        matchType: 'normalizedContains',
        pattern: 'Atmos',
        score: 50,
        mutexGroup: '',
        enabled: true
      },
      {
        name: '7.1',
        matchType: 'normalizedContains',
        pattern: '7.1',
        score: 30,
        mutexGroup: 'Channel Layout',
        enabled: true
      },
      {
        name: '5.1',
        matchType: 'normalizedContains',
        pattern: '5.1',
        score: 15,
        mutexGroup: 'Channel Layout',
        enabled: true
      },
      {
        name: 'TrueHD',
        matchType: 'normalizedContains',
        pattern: 'TrueHD',
        score: 50,
        mutexGroup: 'Lossless Codec',
        enabled: true
      },
      {
        name: 'DTS-HD MA',
        matchType: 'normalizedContains',
        pattern: 'DTS-HD',
        score: 50,
        mutexGroup: 'Lossless Codec',
        enabled: true
      },
      {
        name: 'DDP',
        matchType: 'normalizedContains',
        pattern: 'DDP',
        score: 15,
        mutexGroup: 'Lossy Codec',
        enabled: true
      },
      {
        name: 'DD',
        matchType: 'normalizedContains',
        pattern: 'DD',
        score: 10,
        mutexGroup: 'Lossy Codec',
        enabled: true
      }
    ],
    mutexGroups: [
      {
        name: 'Channel Layout',
        enabled: true
      },
      {
        name: 'Lossless Codec',
        enabled: true
      },
      {
        name: 'Lossy Codec',
        enabled: true
      }
    ]
  };
}

function createDefaultLanguagePreference(audioScoreProfiles) {
  return {
    id: 0,
    name: translate('DefaultAudioLanguagePreferenceName'),
    enabled: true,
    scoreGapThreshold: 35,
    audioScoreProfileId: audioScoreProfiles[0]?.id || null,
    entries: [
      {
        languageTag: 'Chinese',
        enabled: true
      },
      {
        languageTag: 'Origin',
        enabled: true
      }
    ]
  };
}

function cloneLanguageMapping(item) {
  return {
    ...item,
    language: item.language ? { ...item.language } : null,
    aliases: [...(item.aliases || [])]
  };
}

function cloneAudioScoreProfile(item) {
  return {
    ...item,
    rules: (item.rules || []).map((rule) => ({ ...rule })),
    mutexGroups: (item.mutexGroups || []).map((group) => ({ ...group }))
  };
}

function cloneLanguagePreference(item) {
  return {
    ...item,
    entries: (item.entries || []).map((entry) => ({ ...entry }))
  };
}

function AddButton({ label, onPress }) {
  return (
    <Button kind="primary" onPress={onPress}>
      <span className={styles.buttonContent}>
        <Icon name={icons.ADD} size={12} />
        {label}
      </span>
    </Button>
  );
}

function ConfigSection({ legend, addLabel, onAddPress, children }) {
  return (
    <FieldSet legend={legend}>
      <div className={styles.sectionToolbar}>
        <AddButton label={addLabel} onPress={onAddPress} />
      </div>

      {children}
    </FieldSet>
  );
}

function EmptyState({ children }) {
  return <div className={styles.emptyState}>{children}</div>;
}

function ActionButtons({ onEditPress, onDeletePress }) {
  return (
    <div className={styles.actions}>
      <IconButton
        aria-label={translate('Edit')}
        title={translate('Edit')}
        name={icons.EDIT}
        size={14}
        onPress={onEditPress}
      />

      <IconButton
        aria-label={translate('Delete')}
        title={translate('Delete')}
        name={icons.DELETE}
        size={14}
        onPress={onDeletePress}
      />
    </div>
  );
}

function TextField({ label, value, placeholder, onChange }) {
  return (
    <label className={styles.field}>
      <span>{label}</span>
      <input
        className={styles.input}
        type="text"
        value={value || ''}
        placeholder={placeholder}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}

function NumberField({ label, value, onChange }) {
  return (
    <label className={styles.field}>
      <span>{label}</span>
      <input
        className={styles.input}
        type="number"
        value={value ?? 0}
        onChange={(event) => {
          const nextValue = Number.parseInt(event.target.value);
          onChange(Number.isNaN(nextValue) ? 0 : nextValue);
        }}
      />
    </label>
  );
}

function SelectField({ label, value, children, onChange }) {
  return (
    <label className={styles.field}>
      <span>{label}</span>
      <select
        className={styles.input}
        value={value ?? ''}
        onChange={(event) => onChange(event.target.value)}
      >
        {children}
      </select>
    </label>
  );
}

function CheckboxField({ label, checked, onChange }) {
  return (
    <label className={styles.checkboxField}>
      <input
        type="checkbox"
        checked={!!checked}
        onChange={(event) => onChange(event.target.checked)}
      />
      <span>{label}</span>
    </label>
  );
}

function LanguageMappingForm({ draft, languages, setDraft }) {
  const aliases = draft.aliases || [];

  return (
    <>
      <div className={styles.formGrid}>
        <SelectField
          label={translate('Language')}
          value={draft.language?.id ?? ''}
          onChange={(value) => {
            const language = languages.find((item) => item.id === Number(value));
            setDraft((current) => ({
              ...current,
              language: language || current.language
            }));
          }}
        >
          {languages.map((language) => (
            <option key={language.id} value={language.id}>
              {language.name}
            </option>
          ))}
        </SelectField>
      </div>

      <div className={styles.arrayHeader}>
        <h3>{translate('AudioLanguageAliases')}</h3>
        <Button
          size="small"
          onPress={() => {
            setDraft((current) => ({
              ...current,
              aliases: [...(current.aliases || []), '']
            }));
          }}
        >
          {translate('Add')}
        </Button>
      </div>

      <div className={styles.inputList}>
        {aliases.map((alias, index) => (
          <div key={index} className={styles.inputListItem}>
            <input
              className={styles.input}
              type="text"
              value={alias || ''}
              onChange={(event) => {
                setDraft((current) => {
                  const nextAliases = [...(current.aliases || [])];
                  nextAliases[index] = event.target.value;

                  return {
                    ...current,
                    aliases: nextAliases
                  };
                });
              }}
            />

            <IconButton
              aria-label={translate('Delete')}
              title={translate('Delete')}
              name={icons.DELETE}
              size={12}
              onPress={() => {
                setDraft((current) => ({
                  ...current,
                  aliases: (current.aliases || []).filter((_, aliasIndex) => aliasIndex !== index)
                }));
              }}
            />
          </div>
        ))}
      </div>

      <CheckboxField
        label={translate('Enabled')}
        checked={draft.enabled}
        onChange={(value) => {
          setDraft((current) => ({
            ...current,
            enabled: value
          }));
        }}
      />
    </>
  );
}

function AudioScoreProfileForm({ draft, setDraft }) {
  const mutexGroups = draft.mutexGroups || [];
  const rules = draft.rules || [];
  const mutexGroupNames = Array.from(new Set(
    mutexGroups
      .map((group) => `${group.name || ''}`.trim())
      .filter(Boolean)
  ));

  function updateRule(index, field, value) {
    setDraft((current) => {
      const nextRules = [...(current.rules || [])];
      nextRules[index] = {
        ...nextRules[index],
        [field]: value
      };

      return {
        ...current,
        rules: nextRules
      };
    });
  }

  function updateMutexGroup(index, field, value) {
    setDraft((current) => {
      const nextGroups = [...(current.mutexGroups || [])];
      nextGroups[index] = {
        ...nextGroups[index],
        [field]: value
      };

      return {
        ...current,
        mutexGroups: nextGroups
      };
    });
  }

  return (
    <>
      <div className={styles.formGrid}>
        <TextField
          label={translate('Name')}
          value={draft.name}
          onChange={(value) => {
            setDraft((current) => ({
              ...current,
              name: value
            }));
          }}
        />
      </div>

      <CheckboxField
        label={translate('Enabled')}
        checked={draft.enabled}
        onChange={(value) => {
          setDraft((current) => ({
            ...current,
            enabled: value
          }));
        }}
      />

      <div className={styles.arrayHeader}>
        <h3>{translate('AudioScoreRules')}</h3>
        <Button
          size="small"
          onPress={() => {
            setDraft((current) => ({
              ...current,
              rules: [
                ...(current.rules || []),
                {
                  name: '',
                  matchType: 'contains',
                  pattern: '',
                  score: 0,
                  mutexGroup: '',
                  enabled: true
                }
              ]
            }));
          }}
        >
          {translate('Add')}
        </Button>
      </div>

      <div className={styles.formTableScroller}>
        <table className={styles.formTable}>
          <thead>
            <tr>
              <th>{translate('Name')}</th>
              <th>{translate('MatchType')}</th>
              <th>{translate('Pattern')}</th>
              <th>{translate('Score')}</th>
              <th>{translate('MutexGroup')}</th>
              <th>{translate('Enabled')}</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rules.map((rule, index) => (
              <tr key={index}>
                <td>
                  <input
                    className={styles.compactInput}
                    type="text"
                    value={rule.name || ''}
                    onChange={(event) => updateRule(index, 'name', event.target.value)}
                  />
                </td>
                <td>
                  <select
                    className={styles.compactInput}
                    value={rule.matchType || 'contains'}
                    onChange={(event) => updateRule(index, 'matchType', event.target.value)}
                  >
                    {MATCH_TYPES.map((matchType) => (
                      <option key={matchType.key} value={matchType.key}>
                        {matchType.value}
                      </option>
                    ))}
                  </select>
                </td>
                <td>
                  <input
                    className={styles.compactInput}
                    type="text"
                    value={rule.pattern || ''}
                    onChange={(event) => updateRule(index, 'pattern', event.target.value)}
                  />
                </td>
                <td>
                  <input
                    className={styles.scoreInput}
                    type="number"
                    value={rule.score ?? 0}
                    onChange={(event) => {
                      const value = Number.parseInt(event.target.value);
                      updateRule(index, 'score', Number.isNaN(value) ? 0 : value);
                    }}
                  />
                </td>
                <td>
                  <select
                    className={styles.compactInput}
                    value={rule.mutexGroup || ''}
                    onChange={(event) => updateRule(index, 'mutexGroup', event.target.value)}
                  >
                    <option value="">{translate('None')}</option>
                    {
                      [
                        ...(
                          rule.mutexGroup && !mutexGroupNames.includes(rule.mutexGroup) ?
                            [rule.mutexGroup] :
                            []
                        ),
                        ...mutexGroupNames
                      ].map((groupName) => (
                        <option key={groupName} value={groupName}>
                          {groupName}
                        </option>
                      ))
                    }
                  </select>
                </td>
                <td className={styles.checkboxCell}>
                  <input
                    type="checkbox"
                    checked={!!rule.enabled}
                    onChange={(event) => updateRule(index, 'enabled', event.target.checked)}
                  />
                </td>
                <td className={styles.rowButtonCell}>
                  <IconButton
                    aria-label={translate('Delete')}
                    title={translate('Delete')}
                    name={icons.DELETE}
                    size={12}
                    onPress={() => {
                      setDraft((current) => ({
                        ...current,
                        rules: (current.rules || []).filter((_, ruleIndex) => ruleIndex !== index)
                      }));
                    }}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className={styles.arrayHeader}>
        <h3>{translate('AudioScoreMutexGroups')}</h3>
      </div>

      <div className={styles.cardGrid}>
        {mutexGroups.map((group, index) => (
          <Card key={index} className={styles.editableCard}>
            <div className={styles.editableCardHeader}>
              <input
                className={styles.cardInput}
                type="text"
                value={group.name || ''}
                placeholder={translate('Name')}
                onChange={(event) => updateMutexGroup(index, 'name', event.target.value)}
              />

              <IconButton
                aria-label={translate('Delete')}
                title={translate('Delete')}
                name={icons.DELETE}
                size={12}
                onPress={() => {
                  setDraft((current) => ({
                    ...current,
                    mutexGroups: (current.mutexGroups || []).filter((_, groupIndex) => groupIndex !== index)
                  }));
                }}
              />
            </div>

            <label className={styles.cardCheckbox}>
              <input
                type="checkbox"
                checked={!!group.enabled}
                onChange={(event) => updateMutexGroup(index, 'enabled', event.target.checked)}
              />
              <span>{translate('Enabled')}</span>
            </label>
          </Card>
        ))}

        <Card
          className={styles.addCard}
          onPress={() => {
            setDraft((current) => ({
              ...current,
              mutexGroups: [
                ...(current.mutexGroups || []),
                {
                  name: '',
                  enabled: true
                }
              ]
            }));
          }}
        >
          <div className={styles.center}>
            <Icon
              name={icons.ADD}
              size={45}
            />
          </div>
        </Card>
      </div>
    </>
  );
}

function LanguagePreferenceForm({ draft, audioScoreProfiles, setDraft }) {
  const entries = draft.entries || [];

  function updateEntry(index, field, value) {
    setDraft((current) => {
      const nextEntries = [...(current.entries || [])];
      nextEntries[index] = {
        ...nextEntries[index],
        [field]: value
      };

      return {
        ...current,
        entries: nextEntries
      };
    });
  }

  return (
    <>
      <div className={styles.formGrid}>
        <TextField
          label={translate('Name')}
          value={draft.name}
          onChange={(value) => {
            setDraft((current) => ({
              ...current,
              name: value
            }));
          }}
        />

        <NumberField
          label={translate('ScoreGapThreshold')}
          value={draft.scoreGapThreshold}
          onChange={(value) => {
            setDraft((current) => ({
              ...current,
              scoreGapThreshold: value
            }));
          }}
        />

        <SelectField
          label={translate('AudioScoreProfile')}
          value={draft.audioScoreProfileId || ''}
          onChange={(value) => {
            setDraft((current) => ({
              ...current,
              audioScoreProfileId: value ? Number(value) : null
            }));
          }}
        >
          <option value="">{translate('None')}</option>
          {audioScoreProfiles.map((profile) => (
            <option key={profile.id} value={profile.id}>
              {profile.name}
            </option>
          ))}
        </SelectField>
      </div>

      <CheckboxField
        label={translate('Enabled')}
        checked={draft.enabled}
        onChange={(value) => {
          setDraft((current) => ({
            ...current,
            enabled: value
          }));
        }}
      />

      <div className={styles.arrayHeader}>
        <h3>{translate('AudioLanguagePreferenceEntries')}</h3>
        <Button
          size="small"
          onPress={() => {
            setDraft((current) => ({
              ...current,
              entries: [
                ...(current.entries || []),
                {
                  languageTag: '',
                  enabled: true
                }
              ]
            }));
          }}
        >
          {translate('Add')}
        </Button>
      </div>

      <div className={styles.formTableScroller}>
        <table className={styles.formTable}>
          <thead>
            <tr>
              <th>{translate('LanguageTag')}</th>
              <th>{translate('Enabled')}</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {entries.map((entry, index) => (
              <tr key={index}>
                <td>
                  <input
                    className={styles.compactInput}
                    type="text"
                    value={entry.languageTag || ''}
                    onChange={(event) => updateEntry(index, 'languageTag', event.target.value)}
                  />
                </td>
                <td className={styles.checkboxCell}>
                  <input
                    type="checkbox"
                    checked={!!entry.enabled}
                    onChange={(event) => updateEntry(index, 'enabled', event.target.checked)}
                  />
                </td>
                <td className={styles.rowButtonCell}>
                  <IconButton
                    aria-label={translate('Delete')}
                    title={translate('Delete')}
                    name={icons.DELETE}
                    size={12}
                    onPress={() => {
                      setDraft((current) => ({
                        ...current,
                        entries: (current.entries || []).filter((_, entryIndex) => entryIndex !== index)
                      }));
                    }}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function EditConfigModal({
  type,
  draft,
  languages,
  audioScoreProfiles,
  isSaving,
  saveError,
  setDraft,
  onSavePress,
  onModalClose
}) {
  if (!type || !draft) {
    return null;
  }

  let title = '';
  let body = null;

  if (type === 'languageMapping') {
    title = draft.id ? translate('EditAudioLanguageMapping') : translate('AddAudioLanguageMapping');
    body = (
      <LanguageMappingForm
        draft={draft}
        languages={languages}
        setDraft={setDraft}
      />
    );
  } else if (type === 'audioScoreProfile') {
    title = draft.id ? translate('EditAudioScoreProfile') : translate('AddAudioScoreProfile');
    body = (
      <AudioScoreProfileForm
        draft={draft}
        setDraft={setDraft}
      />
    );
  } else if (type === 'languagePreference') {
    title = draft.id ? translate('EditAudioLanguagePreference') : translate('AddAudioLanguagePreference');
    body = (
      <LanguagePreferenceForm
        draft={draft}
        audioScoreProfiles={audioScoreProfiles}
        setDraft={setDraft}
      />
    );
  }

  return (
    <Modal isOpen={true} size="large"
      onModalClose={onModalClose}
    >
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>{title}</ModalHeader>

        <ModalBody>
          {saveError ? <Alert kind="danger">{saveError}</Alert> : null}
          {body}
        </ModalBody>

        <ModalFooter>
          <Button onPress={onModalClose}>{translate('Cancel')}</Button>

          <SpinnerButton
            kind="primary"
            isSpinning={isSaving}
            onPress={onSavePress}
          >
            {translate('Save')}
          </SpinnerButton>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

function ReleaseScoringSettingsPage() {
  const [isFetching, setIsFetching] = useState(true);
  const [loadError, setLoadError] = useState(null);
  const [languages, setLanguages] = useState([]);
  const [languageMappings, setLanguageMappings] = useState([]);
  const [audioScoreProfiles, setAudioScoreProfiles] = useState([]);
  const [languagePreferences, setLanguagePreferences] = useState([]);
  const [modalType, setModalType] = useState(null);
  const [draft, setDraft] = useState(null);
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const audioScoreProfileNames = useMemo(() => {
    return audioScoreProfiles.reduce((acc, profile) => {
      acc[profile.id] = profile.name;
      return acc;
    }, {});
  }, [audioScoreProfiles]);

  const loadData = useCallback(async() => {
    setIsFetching(true);
    setLoadError(null);

    try {
      const [
        languageItems,
        mappingItems,
        audioScoreItems,
        preferenceItems
      ] = await Promise.all([
        requestJson({ url: '/language' }),
        requestJson({ url: ENDPOINTS.languageMapping }),
        requestJson({ url: ENDPOINTS.audioScoreProfile }),
        requestJson({ url: ENDPOINTS.languagePreference })
      ]);

      setLanguages(languageItems || []);
      setLanguageMappings(mappingItems || []);
      setAudioScoreProfiles(audioScoreItems || []);
      setLanguagePreferences(preferenceItems || []);
    } catch (error) {
      setLoadError(getErrorMessage(error, translate('AudioFormatsLoadError')));
    } finally {
      setIsFetching(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  function openModal(type, item) {
    setSaveError(null);
    setModalType(type);

    if (item) {
      if (type === 'languageMapping') {
        setDraft(cloneLanguageMapping(item));
      } else if (type === 'audioScoreProfile') {
        setDraft(cloneAudioScoreProfile(item));
      } else if (type === 'languagePreference') {
        setDraft(cloneLanguagePreference(item));
      }

      return;
    }

    if (type === 'languageMapping') {
      setDraft(createDefaultLanguageMapping(languages));
    } else if (type === 'audioScoreProfile') {
      setDraft(createDefaultAudioScoreProfile());
    } else if (type === 'languagePreference') {
      setDraft(createDefaultLanguagePreference(audioScoreProfiles));
    }
  }

  const closeModal = useCallback(() => {
    if (isSaving) {
      return;
    }

    setModalType(null);
    setDraft(null);
    setSaveError(null);
  }, [isSaving]);

  const saveDraft = useCallback(async() => {
    if (!modalType || !draft) {
      return;
    }

    setIsSaving(true);
    setSaveError(null);

    try {
      const endpoint = ENDPOINTS[modalType];
      const id = draft.id;
      const method = id ? 'PUT' : 'POST';
      const url = id ? `${endpoint}/${id}` : endpoint;
      const data = modalType === 'languageMapping' ? {
        ...draft,
        aliases: (draft.aliases || [])
          .map((alias) => `${alias || ''}`.trim())
          .filter(Boolean)
      } : draft;

      await requestJson({
        url,
        method,
        data
      });

      setModalType(null);
      setDraft(null);
      await loadData();
    } catch (error) {
      setSaveError(getErrorMessage(error, translate('AudioFormatsSaveError')));
    } finally {
      setIsSaving(false);
    }
  }, [draft, loadData, modalType]);

  const confirmDelete = useCallback(async() => {
    if (!deleteTarget) {
      return;
    }

    setIsDeleting(true);

    try {
      await requestJson({
        url: `${ENDPOINTS[deleteTarget.type]}/${deleteTarget.item.id}`,
        method: 'DELETE'
      });

      setDeleteTarget(null);
      await loadData();
    } catch (error) {
      setLoadError(getErrorMessage(error, translate('AudioFormatsDeleteError')));
    } finally {
      setIsDeleting(false);
    }
  }, [deleteTarget, loadData]);

  const cancelDelete = useCallback(() => {
    setDeleteTarget(null);
  }, []);

  const deleteName = deleteTarget?.item.name ||
    deleteTarget?.item.language?.name ||
    translate('SelectedItem');

  return (
    <PageContent title={translate('AudioFormatsSettings')}>
      <SettingsToolbar showSave={false} hasPendingChanges={false} />

      <PageContentBody>
        {loadError ? <Alert kind="danger">{loadError}</Alert> : null}

        {isFetching ? (
          <LoadingIndicator />
        ) : (
          <>
            <ConfigSection
              legend={translate('AudioLanguageMappings')}
              addLabel={translate('AddAudioLanguageMapping')}
              onAddPress={() => openModal('languageMapping')}
            >
              {languageMappings.length ? (
                <div className={styles.tableScroller}>
                  <table className={styles.table}>
                    <thead>
                      <tr>
                        <th>{translate('Language')}</th>
                        <th>{translate('AudioLanguageAliases')}</th>
                        <th>{translate('Status')}</th>
                        <th />
                      </tr>
                    </thead>
                    <tbody>
                      {languageMappings.map((mapping) => (
                        <tr key={mapping.id}>
                          <td className={styles.nameCell}>{getLanguageName(mapping.language)}</td>
                          <td>{(mapping.aliases || []).join(', ')}</td>
                          <td>{mapping.enabled ? translate('Enabled') : translate('Disabled')}</td>
                          <td className={styles.actionsCell}>
                            <ActionButtons
                              onEditPress={() => openModal('languageMapping', mapping)}
                              onDeletePress={() => setDeleteTarget({ type: 'languageMapping', item: mapping })}
                            />
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <EmptyState>{translate('NoAudioLanguageMappings')}</EmptyState>
              )}
            </ConfigSection>

            <ConfigSection
              legend={translate('AudioScoreProfiles')}
              addLabel={translate('AddAudioScoreProfile')}
              onAddPress={() => openModal('audioScoreProfile')}
            >
              {audioScoreProfiles.length ? (
                <div className={styles.tableScroller}>
                  <table className={styles.table}>
                    <thead>
                      <tr>
                        <th>{translate('Name')}</th>
                        <th>{translate('AudioScoreRules')}</th>
                        <th>{translate('AudioScoreMutexGroups')}</th>
                        <th>{translate('Status')}</th>
                        <th />
                      </tr>
                    </thead>
                    <tbody>
                      {audioScoreProfiles.map((profile) => (
                        <tr key={profile.id}>
                          <td className={styles.nameCell}>{profile.name}</td>
                          <td>{(profile.rules || []).length}</td>
                          <td>{(profile.mutexGroups || []).map((group) => group.name).join(', ')}</td>
                          <td>{profile.enabled ? translate('Enabled') : translate('Disabled')}</td>
                          <td className={styles.actionsCell}>
                            <ActionButtons
                              onEditPress={() => openModal('audioScoreProfile', profile)}
                              onDeletePress={() => setDeleteTarget({ type: 'audioScoreProfile', item: profile })}
                            />
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <EmptyState>{translate('NoAudioScoreProfiles')}</EmptyState>
              )}
            </ConfigSection>

            <ConfigSection
              legend={translate('AudioLanguagePreferences')}
              addLabel={translate('AddAudioLanguagePreference')}
              onAddPress={() => openModal('languagePreference')}
            >
              {languagePreferences.length ? (
                <div className={styles.tableScroller}>
                  <table className={styles.table}>
                    <thead>
                      <tr>
                        <th>{translate('Name')}</th>
                        <th>{translate('AudioLanguagePreferenceEntries')}</th>
                        <th>{translate('ScoreGapThreshold')}</th>
                        <th>{translate('AudioScoreProfile')}</th>
                        <th>{translate('Status')}</th>
                        <th />
                      </tr>
                    </thead>
                    <tbody>
                      {languagePreferences.map((preference) => (
                        <tr key={preference.id}>
                          <td className={styles.nameCell}>{preference.name}</td>
                          <td>{(preference.entries || []).map((entry) => entry.languageTag).join(', ')}</td>
                          <td>{preference.scoreGapThreshold}</td>
                          <td>{audioScoreProfileNames[preference.audioScoreProfileId] || translate('None')}</td>
                          <td>{preference.enabled ? translate('Enabled') : translate('Disabled')}</td>
                          <td className={styles.actionsCell}>
                            <ActionButtons
                              onEditPress={() => openModal('languagePreference', preference)}
                              onDeletePress={() => setDeleteTarget({ type: 'languagePreference', item: preference })}
                            />
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <EmptyState>{translate('NoAudioLanguagePreferences')}</EmptyState>
              )}
            </ConfigSection>

          </>
        )}

        <EditConfigModal
          type={modalType}
          draft={draft}
          languages={languages}
          audioScoreProfiles={audioScoreProfiles}
          isSaving={isSaving}
          saveError={saveError}
          setDraft={setDraft}
          onSavePress={saveDraft}
          onModalClose={closeModal}
        />

        <ConfirmModal
          isOpen={!!deleteTarget}
          kind="danger"
          title={translate('Delete')}
          message={translate('DeleteAudioFormatsConfigMessage', { name: deleteName })}
          confirmLabel={translate('Delete')}
          cancelLabel={translate('Cancel')}
          isSpinning={isDeleting}
          onConfirm={confirmDelete}
          onCancel={cancelDelete}
        />
      </PageContentBody>
    </PageContent>
  );
}

export default ReleaseScoringSettingsPage;
